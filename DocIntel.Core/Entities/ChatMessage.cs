namespace DocIntel.Core.Entities;

public class ChatMessage : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }

    // "user" or "assistant" — matches OpenAI's convention
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    // JSON array of chunk IDs used to generate this answer — for citations
    public string? SourceChunksJson { get; set; }

    // Navigation
    public Document Document { get; set; } = null!;
    public AppUser User { get; set; } = null!;
}