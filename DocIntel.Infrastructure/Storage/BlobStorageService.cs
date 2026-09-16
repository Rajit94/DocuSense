using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DocIntel.Core.Interfaces;

namespace DocIntel.Infrastructure.Storage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobStorageService(
        BlobServiceClient blobServiceClient,
        string containerName = "documents")
    {
        _blobServiceClient = blobServiceClient;
        _containerName = containerName;
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType)
    {
      
        var containerClient = _blobServiceClient
            .GetBlobContainerClient(_containerName);

        
        await containerClient.CreateIfNotExistsAsync();

        var blobName = $"{Guid.NewGuid()}-{SanitizeFileName(fileName)}";

        
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
        {
            ContentType = contentType
        });

         https://docusensestorage.blob.core.windows.net/documents/a3f2-contract.pdf
        return blobClient.Uri.ToString();
    }

    
    public string GetSignedUrl(string blobUrl, TimeSpan expiry)
    {
        
        var blobName = ExtractBlobNameFromUrl(blobUrl);

        var containerClient = _blobServiceClient
            .GetBlobContainerClient(_containerName);

        var blobClient = containerClient.GetBlobClient(blobName);

       
        var sasUri = blobClient.GenerateSasUri(
            Azure.Storage.Sas.BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.Add(expiry));

        return sasUri.ToString();
    }


    public async Task DeleteAsync(string blobUrl)
    {
        var blobName = ExtractBlobNameFromUrl(blobUrl);

        var containerClient = _blobServiceClient
            .GetBlobContainerClient(_containerName);

        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync();
    }

    
    // Input:  "https://storage.blob.core.windows.net/documents/abc-contract.pdf"
    
    private string ExtractBlobNameFromUrl(string blobUrl)
    {
        var uri = new Uri(blobUrl);

        return uri.Segments.Last();
    }

    private string SanitizeFileName(string fileName)
    {
        
        var sanitized = fileName.Replace(" ", "-");

        sanitized = new string(sanitized
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '.' || c == '_')
            .ToArray());

        return sanitized.ToLower(); 
    }
}