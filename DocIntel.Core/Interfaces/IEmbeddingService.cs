namespace DocIntel.Core.Interfaces;

public interface IEmbeddingService
{
    // Takes text, returns a vector (array of floats)
    // This is what we'll call OpenAI's text-embedding-3-small with
    Task<float[]> GetEmbeddingAsync(string text);

    // Batch version — more efficient for embedding many chunks at once
    Task<IEnumerable<float[]>> GetEmbeddingsAsync(IEnumerable<string> texts);
}