using System.ComponentModel.DataAnnotations;

namespace DocIntel.API.DTOs;
public class ChatRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(2000, ErrorMessage = "Question must be under 2000 characters.")]
    public string Question { get; set; } = string.Empty;
}

public class ChatErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Detail { get; set; }
}