using DocIntel.Core.Entities;
using DocIntel.Core.Interfaces;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;


using AppChatMessage = DocIntel.Core.Entities.ChatMessage;
using OaiChatMessage = OpenAI.Chat.ChatMessage;
using OpenAI.Chat;

namespace DocIntel.Infrastructure.AI;

public class RagService : IRagService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ChatClient _chatClient;

    private const int TopKChunks = 5;
    private const float SimilarityThreshold = 0.70f;
    private const int ChatHistoryLimit = 10;

    public RagService(
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        ChatClient chatClient)
    {
        _embeddingService = embeddingService;
        _documentRepository = documentRepository;
        _chatClient = chatClient;
    }

    public async IAsyncEnumerable<string> AskAsync(
        string question,
        Guid documentId,
        Guid workspaceId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // STEP 1: Embed the question
        var questionEmbedding = await _embeddingService
            .GetEmbeddingAsync(question);

        // STEP 2: Load all chunks for this document
        var allChunks = await _documentRepository
            .GetChunksByDocumentIdAsync(documentId);

        // STEP 3: Score each chunk by similarity
        var scoredChunks = allChunks
            .Where(c => !string.IsNullOrEmpty(c.EmbeddingJson))
            .Select(chunk =>
            {
                var chunkEmbedding = EmbeddingService
                    .DeserializeEmbedding(chunk.EmbeddingJson);

                var similarity = EmbeddingService
                    .CosineSimilarity(questionEmbedding, chunkEmbedding);

                return new { Chunk = chunk, Score = similarity };
            })
            .Where(x => x.Score >= SimilarityThreshold)
            .OrderByDescending(x => x.Score)
            .Take(TopKChunks)
            .ToList();

        // STEP 4: Handle no relevant chunks found
        if (scoredChunks.Count == 0)
        {
            yield return "I couldn't find relevant information in this " +
                         "document to answer your question. Please try " +
                         "rephrasing or ask about a different topic.";
            yield break;
        }

        // STEP 5: Load recent chat history
        // These are our AppChatMessage entities from the DB
        var chatHistory = await _documentRepository
            .GetChatHistoryAsync(documentId, ChatHistoryLimit);

        // STEP 6: Build the OpenAI message list
        // Returns List<OaiChatMessage> — OpenAI's type, not our entity
        var messages = BuildMessages(
            question,
            scoredChunks.Select(x => x.Chunk).ToList(),
            chatHistory);

        // STEP 7: Stream GPT-4o response
        var fullAnswer = new StringBuilder();

        await foreach (var update in _chatClient
            .CompleteChatStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    fullAnswer.Append(part.Text);
                    yield return part.Text;
                }
            }
        }

        // STEP 8: Save the conversation using our AppChatMessage entity
        await _documentRepository.SaveChatMessageAsync(new AppChatMessage
        {
            DocumentId = documentId,
            WorkspaceId = workspaceId,
            Role = "user",
            Content = question,
            SourceChunksJson = null
        });

        var sourceChunkIds = scoredChunks
            .Select(x => new
            {
                chunkId = x.Chunk.Id,
                pageNumber = x.Chunk.PageNumber,
                score = Math.Round(x.Score, 3)
            });

        await _documentRepository.SaveChatMessageAsync(new AppChatMessage
        {
            DocumentId = documentId,
            WorkspaceId = workspaceId,
            Role = "assistant",
            Content = fullAnswer.ToString(),
            SourceChunksJson = JsonSerializer.Serialize(sourceChunkIds)
        });
    }

   
    private static List<OaiChatMessage> BuildMessages(
        string question,
        List<DocumentChunk> relevantChunks,
        IEnumerable<AppChatMessage> history)  
    {
      
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("DOCUMENT CONTEXT:");
        contextBuilder.AppendLine("=================");

        foreach (var chunk in relevantChunks)
        {
            contextBuilder.AppendLine($"[Page {chunk.PageNumber}]");
            contextBuilder.AppendLine(chunk.Text);
            contextBuilder.AppendLine();
        }

        var systemPrompt = $"""
            You are an expert document analyst for DocuSense, an AI document 
            intelligence platform. Your job is to answer questions about 
            documents accurately and helpfully.

            STRICT RULES — follow these exactly:
            1. Answer ONLY using the document context provided below.
            2. If the answer is not in the context, say exactly:
               "I couldn't find this information in the provided document sections."
            3. ALWAYS cite the page number(s) you used: e.g. "(Page 4)" or "(Pages 2, 7)"
            4. Be concise and direct — legal and business users need clear answers.
            5. Never make up information or use knowledge outside the provided context.

            {contextBuilder}
            """;

      
        var messages = new List<OaiChatMessage>
        {
           
            new SystemChatMessage(systemPrompt)
        };

     
        foreach (var historyMessage in history)
        {
            if (historyMessage.Role == "user")
                messages.Add(new UserChatMessage(historyMessage.Content));
            else
                messages.Add(new AssistantChatMessage(historyMessage.Content));
        }

        messages.Add(new UserChatMessage(question));

        return messages;
    }
}