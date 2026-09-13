namespace DocIntel.Core.Interfaces;

public interface IBlobStorageService
{
    // Upload a file stream — returns the blob URL
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);

    // Delete a blob by its URL
    Task DeleteAsync(string blobUrl);

    // Get a short-lived signed URL for secure access (expires in 1 hour)
    string GetSignedUrl(string blobUrl, TimeSpan expiry);
}