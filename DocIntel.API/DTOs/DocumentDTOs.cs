namespace DocIntel.API.DTOs;


public class DocumentSummaryResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public float? RiskScore { get; set; }
    public string? Summary { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}


public class DocumentDetailResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public float? RiskScore { get; set; }
    public string? Summary { get; set; }
    public string? AnalysisJson { get; set; }
    public string SignedUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
}


public class UploadResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}