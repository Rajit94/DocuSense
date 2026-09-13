namespace DocIntel.Core.Interfaces;

public interface IRagService
{
    // Takes a question + documentId, returns an async stream of answer tokens
    // IAsyncEnumerable = the .NET way to stream data — one token at a time
    IAsyncEnumerable<string> AskAsync(
        string question,
        Guid documentId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}