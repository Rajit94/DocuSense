using System.ComponentModel.DataAnnotations;

namespace DocIntel.API.DTOs;

public class RegisterRequest
{
    [Required]
    [MaxLength(100)]
    public string WorkspaceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string WorkspaceSlug { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;
}