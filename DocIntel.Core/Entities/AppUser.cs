using DocIntel.Core.Enums;

namespace DocIntel.Core.Entities;

public class AppUser : BaseEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Member;

   
    public Workspace Workspace { get; set; } = null!;
}