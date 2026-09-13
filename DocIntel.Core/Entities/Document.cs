using DocIntel.Core.Enums;

namespace DocIntel.Core.Entities;

public class Document : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string BlobUrl { get; set; } = string.Empty;      // Azure Blob Storage URL
    public string ContentType { get; set; } = string.Empty;  // "application/pdf" etc.
    public long FileSizeBytes { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    // Populated after AI analysis job completes
    public string? Summary { get; set; }           // nullable — not set until analysed
    public float? RiskScore { get; set; }          // 0.0 to 1.0
    public string? AnalysisJson { get; set; }      // full JSON from GPT-4o function call

    public Guid UploadedByUserId { get; set; }

    // Navigation properties
    public Workspace Workspace { get; set; } = null!;
    public AppUser UploadedBy { get; set; } = null!;
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}