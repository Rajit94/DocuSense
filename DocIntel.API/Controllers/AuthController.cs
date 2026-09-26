using DocIntel.API.DTOs;
using DocIntel.Core.Entities;
using DocIntel.Core.Enums;
using DocIntel.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DocIntel.API.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    
    public AuthController(
        IDbContextFactory<AppDbContext> contextFactory,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _contextFactory = contextFactory;
        _configuration = configuration;
        _logger = logger;
    }

   
    [HttpPost("register")]
    [AllowAnonymous] 
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request)
    {
       
        await using var context = _contextFactory.CreateDbContext();


        var slugExists = await context.Workspaces
            .IgnoreQueryFilters() 
            .AnyAsync(w => w.Slug == request.WorkspaceSlug.ToLower());

        if (slugExists)
        {
            
            return Conflict(new { error = "This workspace URL is already taken." });
        }

        
        var emailExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email.ToLower());

        if (emailExists)
        {
            return Conflict(new { error = "An account with this email already exists." });
        }

        
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.WorkspaceName.Trim(),
            Slug = request.WorkspaceSlug.ToLower().Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await context.Workspaces.AddAsync(workspace);


        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Email = request.Email.ToLower().Trim(),
            PasswordHash = HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Admin, 
            CreatedAt = DateTime.UtcNow
        };

        await context.Users.AddAsync(user);

     
        await context.SaveChangesAsync();

        _logger.LogInformation(
            "New workspace '{WorkspaceName}' registered by {Email}",
            workspace.Name, user.Email);

      
        var token = GenerateJwt(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            Role = user.Role.ToString(),
            WorkspaceId = user.WorkspaceId,
            ExpiresAt = DateTime.UtcNow.AddHours(
                int.Parse(_configuration["Jwt:ExpiryHours"] ?? "24"))
        });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request)
    {
        await using var context = _contextFactory.CreateDbContext();

       
        var user = await context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Workspace)
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

        if (user is null || !VerifyPassword(request.Password, user.PasswordHash))
        {
         
            return Unauthorized(new { error = "Invalid email or password." });
        }

        _logger.LogInformation(
            "User {Email} logged in successfully", user.Email);

        var token = GenerateJwt(user);

        return Ok(new AuthResponse
        {
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            Role = user.Role.ToString(),
            WorkspaceId = user.WorkspaceId,
            ExpiresAt = DateTime.UtcNow.AddHours(
                int.Parse(_configuration["Jwt:ExpiryHours"] ?? "24"))
        });
    }

 
    [HttpGet("me")]
    [Authorize] 
    public async Task<ActionResult<AuthResponse>> Me()
    {
        
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        await using var context = _contextFactory.CreateDbContext();

        var user = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
            return NotFound();

   
        return Ok(new AuthResponse
        {
            Token = string.Empty,
            Email = user.Email,
            FirstName = user.FirstName,
            Role = user.Role.ToString(),
            WorkspaceId = user.WorkspaceId,
            ExpiresAt = DateTime.UtcNow
        });
    }

    private string GenerateJwt(AppUser user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

       
        var claims = new[]
        {
           
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),

         
            new Claim(ClaimTypes.Email, user.Email),

           
            new Claim(ClaimTypes.Role, user.Role.ToString()),

            
            new Claim("WorkspaceId", user.WorkspaceId.ToString()),

            
            new Claim("FirstName", user.FirstName),
        };

        var expiryHours = int.Parse(
            _configuration["Jwt:ExpiryHours"] ?? "24");

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
           
            notBefore: DateTime.UtcNow,
           
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials
        );

      
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

   
    private static string HashPassword(string password)
    {
      
        byte[] salt = RandomNumberGenerator.GetBytes(16);

       
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32);

       
        return $"{Convert.ToBase64String(salt)}:" +
               $"{Convert.ToBase64String(hash)}";
    }

    
    private static bool VerifyPassword(string password, string storedHash)
    {
        try
        {
          
            var parts = storedHash.Split(':');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);

           
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations: 100_000,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: 32);

         
            return CryptographicOperations.FixedTimeEquals(
                actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}