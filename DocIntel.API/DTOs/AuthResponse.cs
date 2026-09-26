namespace DocIntel.API.DTOs;

// This is what we return to the client after successful
// register or login. The frontend stores the Token and
// sends it as "Authorization: Bearer {Token}" on every request.
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
    public DateTime ExpiresAt { get; set; }
}