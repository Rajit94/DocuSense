using DocIntel.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly Guid _workspaceId;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        Guid workspaceId)
        : base(options)
    {
        _workspaceId = workspaceId;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        _workspaceId = Guid.Empty;
    }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Document>()
            .HasQueryFilter(d => d.WorkspaceId == _workspaceId);

        modelBuilder.Entity<DocumentChunk>()
            .HasQueryFilter(c => c.WorkspaceId == _workspaceId);

        modelBuilder.Entity<ChatMessage>()
            .HasQueryFilter(m => m.WorkspaceId == _workspaceId);

        modelBuilder.Entity<AppUser>()
            .HasQueryFilter(u => u.WorkspaceId == _workspaceId);

        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.HasIndex(w => w.Slug).IsUnique();
            entity.Property(w => w.Name).IsRequired().HasMaxLength(100);
            entity.Property(w => w.Slug).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.PasswordHash).IsRequired();

            entity.HasOne(u => u.Workspace)
                .WithMany(w => w.Users)
                .HasForeignKey(u => u.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(u => u.Role)
                .HasConversion<string>();
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(500);
            entity.Property(d => d.BlobUrl).IsRequired();
            entity.Property(d => d.ContentType).IsRequired().HasMaxLength(100);

            entity.Property(d => d.Status)
                .HasConversion<string>();

            entity.HasOne(d => d.Workspace)
                .WithMany(w => w.Documents)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.Chunks)
                .WithOne(c => c.Document)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(d => d.ChatMessages)
                .WithOne(m => m.Document)
                .HasForeignKey(m => m.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.DocumentId);
            entity.HasIndex(c => c.WorkspaceId);
            entity.Property(c => c.EmbeddingJson)
                .HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).IsRequired().HasMaxLength(20);
            entity.Property(m => m.Content).IsRequired();
            entity.HasIndex(m => m.DocumentId);
        });
    }
}