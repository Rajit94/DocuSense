using DocIntel.Core.Entities;

namespace DocIntel.Infrastructure.Processing;

public class TextChunker
{
    private const int MaxChunkTokens = 400;
    private const int OverlapTokens = 50;
    private const int CharsPerToken = 4;

    private readonly int _maxChunkChars = MaxChunkTokens * CharsPerToken;
    private readonly int _overlapChars = OverlapTokens * CharsPerToken;

    public List<DocumentChunk> Chunk(
        List<PdfExtractor.ExtractedPage> pages,
        Guid documentId,
        Guid workspaceId)
    {
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var page in pages)
        {
            var pageChunks = ChunkText(page.Text);

            foreach (var chunkText in pageChunks)
            {
                chunks.Add(new DocumentChunk
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    WorkspaceId = workspaceId,
                    Text = chunkText,
                    PageNumber = page.PageNumber,
                    ChunkIndex = chunkIndex++,
                    EmbeddingJson = string.Empty
                });
            }
        }

        return chunks;
    }

    public List<DocumentChunk> Chunk(
        List<DocxExtractor.ExtractedPage> pages,
        Guid documentId,
        Guid workspaceId)
    {
        var converted = pages
            .Select(p => new PdfExtractor.ExtractedPage(p.PageNumber, p.Text))
            .ToList();

        return Chunk(converted, documentId, workspaceId);
    }

    private List<string> ChunkText(string text)
    {
        var chunks = new List<string>();
        var sentences = SplitIntoSentences(text);
        var currentChunk = new System.Text.StringBuilder();

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length > _maxChunkChars
                && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());

                var overlap = GetOverlapText(currentChunk.ToString());
                currentChunk.Clear();
                currentChunk.Append(overlap);
            }

            currentChunk.Append(sentence);
            currentChunk.Append(' ');
        }

        if (currentChunk.Length > 0)
        {
            var finalChunk = currentChunk.ToString().Trim();

            if (finalChunk.Length > 20)
                chunks.Add(finalChunk);
        }

        return chunks;
    }

    private List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            current.Append(text[i]);

            bool isSentenceEnd =
                text[i] == '.' ||
                text[i] == '!' ||
                text[i] == '?';

            bool isFollowedBySpace =
                (i + 1 < text.Length && text[i + 1] == ' ') ||
                i == text.Length - 1;

            if (isSentenceEnd && isFollowedBySpace && current.Length > 10)
            {
                sentences.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
            sentences.Add(current.ToString());

        return sentences;
    }

    private string GetOverlapText(string text)
    {
        if (text.Length <= _overlapChars)
            return text;

        var overlap = text[^_overlapChars..];

        var firstSpace = overlap.IndexOf(' ');

        if (firstSpace > 0)
            overlap = overlap[firstSpace..];

        return overlap;
    }
}