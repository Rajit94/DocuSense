using DocIntel.API.BackgroundJobs;
using DocIntel.API.DTOs;
using DocIntel.Core.Entities;
using DocIntel.Core.Enums;
using DocIntel.Core.Interfaces;
using DocIntel.Infrastructure.Data;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace DocIntel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<DocumentsController> _logger;
    private static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    ];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;
    public DocumentsController(
        IDocumentRepository documentRepository,
        IBlobStorageService blobStorageService,
        IDbContextFactory<AppDbContext> contextFactory,
        IBackgroundJobClient backgroundJobClient,
        ILogger<DocumentsController> logger)
    {
        _documentRepository = documentRepository;
        _blobStorageService = blobStorageService;
        _contextFactory = contextFactory;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }
    [HttpPost("upload")]
    public async Task<ActionResult<UploadResponse>> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });
        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new
            {
                error = $"File size exceeds the 50MB limit. " +
                        $"Your file is {file.Length / 1024 / 1024}MB."
            });
        if (!AllowedContentTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(new
            {
                error = "Only PDF and Word (.docx) files are supported.",
                received = file.ContentType
            });
        var workspaceId = GetWorkspaceId();
        var userId = GetUserId();
        if (workspaceId == Guid.Empty || userId == Guid.Empty)
            return Unauthorized();
        string blobUrl;
        try
        {
            await using var stream = file.OpenReadStream();
            blobUrl = await _blobStorageService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to upload file {FileName} to blob storage",
                file.FileName);
            return StatusCode(500, new
            {
                error = "File upload failed. Please try again."
            });
        }
        var document = new Document
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            UploadedByUserId = userId,
            FileName = file.FileName,
            BlobUrl = blobUrl,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            Status = DocumentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _documentRepository.CreateAsync(document);
        _backgroundJobClient.Enqueue<DocumentProcessingJob>(
            job => job.ProcessAsync(document.Id, workspaceId));
        _logger.LogInformation(
            "Document {FileName} uploaded and queued for processing. " +
            "DocumentId: {DocumentId}",
            file.FileName,
            document.Id);
        return Accepted(new UploadResponse
        {
            DocumentId = document.Id,
            FileName = file.FileName,
            Status = DocumentStatus.Pending.ToString(),
            Message = "Document uploaded successfully. Processing has started."
        });
    }
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentSummaryResponse>>> GetAll()
    {
        var workspaceId = GetWorkspaceId();
        if (workspaceId == Guid.Empty)
            return Unauthorized();
        var documents = await _documentRepository.GetAllAsync(workspaceId);
        var response = documents.Select(d => new DocumentSummaryResponse
        {
            Id = d.Id,
            FileName = d.FileName,
            ContentType = d.ContentType,
            FileSizeBytes = d.FileSizeBytes,
            Status = d.Status.ToString(),
            RiskScore = d.RiskScore,
            Summary = d.Summary,
            CreatedAt = d.CreatedAt,
            UploadedBy = d.UploadedBy?.FirstName ?? "Unknown"
        });
        return Ok(response);
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDetailResponse>> GetById(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        if (workspaceId == Guid.Empty)
            return Unauthorized();
        var document = await _documentRepository.GetByIdAsync(id, workspaceId);
        if (document is null)
            return NotFound(new { error = "Document not found." });
        var signedUrl = _blobStorageService.GetSignedUrl(
            document.BlobUrl,
            TimeSpan.FromHours(1));
        var chunks = await _documentRepository.GetChunksByDocumentIdAsync(id);
        return Ok(new DocumentDetailResponse
        {
            Id = document.Id,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSizeBytes = document.FileSizeBytes,
            Status = document.Status.ToString(),
            RiskScore = document.RiskScore,
            Summary = document.Summary,
            AnalysisJson = document.AnalysisJson,
            SignedUrl = signedUrl,
            CreatedAt = document.CreatedAt,
            UploadedBy = document.UploadedBy?.FirstName ?? "Unknown",
            ChunkCount = chunks.Count()
        });
    }
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult> GetStatus(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        if (workspaceId == Guid.Empty)
            return Unauthorized();
        var document = await _documentRepository.GetByIdAsync(id, workspaceId);
        if (document is null)
            return NotFound(new { error = "Document not found." });
        return Ok(new
        {
            documentId = document.Id,
            status = document.Status.ToString(),
            isReady = document.Status == DocumentStatus.Ready,
            hasFailed = document.Status == DocumentStatus.Failed
        });
    }
    [HttpGet("{id:guid}/chat-history")]
    public async Task<ActionResult> GetChatHistory(
        Guid id,
        [FromQuery] int limit = 20)
    {
        var workspaceId = GetWorkspaceId();
        if (workspaceId == Guid.Empty)
            return Unauthorized();
        var document = await _documentRepository.GetByIdAsync(id, workspaceId);
        if (document is null)
            return NotFound(new { error = "Document not found." });
        var messages = await _documentRepository.GetChatHistoryAsync(id, limit);
        var response = messages.Select(m => new
        {
            id = m.Id,
            role = m.Role,
            content = m.Content,
            sourceChunksJson = m.SourceChunksJson,
            createdAt = m.CreatedAt
        });
        return Ok(response);
    }
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var workspaceId = GetWorkspaceId();
        var userId = GetUserId();
        if (workspaceId == Guid.Empty)
            return Unauthorized();
        var document = await _documentRepository.GetByIdAsync(id, workspaceId);
        if (document is null)
            return NotFound(new { error = "Document not found." });
        var isAdmin = User.IsInRole("Admin");
        var isOwner = document.UploadedByUserId == userId;
        if (!isAdmin && !isOwner)
            return Forbid();
        try
        {
            await _blobStorageService.DeleteAsync(document.BlobUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete blob for document {DocumentId}",
                id);
        }
        await _documentRepository.DeleteAsync(id, workspaceId);
        _logger.LogInformation(
            "Document {DocumentId} deleted by user {UserId}",
            id,
            userId);
        return NoContent();
    }
    private Guid GetWorkspaceId()
    {
        var claim = User.FindFirst("WorkspaceId")?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}