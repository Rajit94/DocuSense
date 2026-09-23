using DocIntel.Core.Interfaces;
using OpenAI;
using OpenAI.Embeddings;
using System.Text.Json;

namespace DocIntel.Infrastructure.AI;

public class EmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _embeddingClient;

    private const int BatchSize = 100;

    public EmbeddingService(EmbeddingClient embeddingClient)
    {
        _embeddingClient = embeddingClient;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var sanitizedText = text.Trim();

        if (string.IsNullOrEmpty(sanitizedText))
            throw new ArgumentException("Cannot embed empty text.", nameof(text));

        var response = await _embeddingClient
            .GenerateEmbeddingAsync(sanitizedText);

        return response.Value.ToFloats().ToArray();
    }

    public async Task<IEnumerable<float[]>> GetEmbeddingsAsync(
        IEnumerable<string> texts)
    {
        var textList = texts
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();

        if (textList.Count == 0)
            return Enumerable.Empty<float[]>();

        var allEmbeddings = new List<float[]>();

        var batches = textList.Chunk(BatchSize);

        foreach (var batch in batches)
        {
            var response = await _embeddingClient
                .GenerateEmbeddingsAsync(batch.ToList());

            var embeddings = response.Value
                .Select(e => e.ToFloats().ToArray());

            allEmbeddings.AddRange(embeddings);

            if (batches.Count() > 1)
                await Task.Delay(100);
        }

        return allEmbeddings;
    }

    public static string SerializeEmbedding(float[] embedding)
        => JsonSerializer.Serialize(embedding);

    public static float[] DeserializeEmbedding(string json)
        => JsonSerializer.Deserialize<float[]>(json)
           ?? Array.Empty<float>();

    public static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have the same dimensions.");

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = MathF.Sqrt(magnitudeA);
        magnitudeB = MathF.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }
}