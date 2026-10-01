using DocIntel.API.DTOs;
using DocIntel.Core.Enums;
using DocIntel.Core.Interfaces;
using DocIntel.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DocIntel.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IRagService _ragService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
    IRagService ragService,
    IDocumentRepository documentRepository,
    ILogger<ChatController> logger)
    {
        _ragService = ragService;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    [HttpPost("{documentId:guid}/stream")]
    public async Task Stream(
    Guid documentId,
    [FromBody] ChatRequest request,
    CancellationToken cancellationToken)
    {
        var workspaceId = GetWorkspaceId();
        var userId = GetUserId();

        if (workspaceId == Guid.Empty || userId == Guid.Empty)
        {
            Response.StatusCode = 401;
            await Response.WriteAsync("Unauthorized.", cancellationToken);
            return;
        }

        var document = await _documentRepository
        .GetByIdAsync(documentId, workspaceId);

        if (document is null)
        {
            Response.StatusCode = 404;
            await Response.WriteAsync(
            JsonSerializer.Serialize(
            new ChatErrorResponse
            {
                Error = "Document not found.",
                Detail = "The document may have been deleted " +
            "or doesn't belong to your workspace."
            }),
            cancellationToken);
            return;
        }

        if (document.Status != DocumentStatus.Ready)
        {
            Response.StatusCode = 409;
            await Response.WriteAsync(
            JsonSerializer.Serialize(
            new ChatErrorResponse
            {
                Error = "Document is not ready yet.",
                Detail = $"Current status: {document.Status}. " +
            "Please wait for processing to complete."
            }),
            cancellationToken);
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        Response.Body.Flush();

        _logger.LogInformation(
        "Chat stream started for document {DocumentId} " +
        "with question: {Question}",
        documentId,
        request.Question[..Math.Min(50, request.Question.Length)]);

        try
        {
            await foreach (var token in _ragService.AskAsync(
            request.Question,
            documentId,
            workspaceId,
            cancellationToken))
            {
                var sseMessage = $"data: {EscapeForSse(token)}\n\n";

                await Response.WriteAsync(sseMessage, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            _logger.LogInformation(
            "Chat stream completed for document {DocumentId}", documentId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
            "Chat stream cancelled for document {DocumentId} " +
            "— client disconnected", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
            "Chat stream failed for document {DocumentId}", documentId);

            try
            {
                var errorEvent = $"event: error\ndata: " +
                $"An error occurred while generating " +
                $"the response.\n\n";

                await Response.WriteAsync(errorEvent, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch
            {
            }
        }
    }

    [HttpGet("{documentId:guid}/history")]
    public async Task<ActionResult> GetHistory(
    Guid documentId,
    [FromQuery] int limit = 20)
    {
        var workspaceId = GetWorkspaceId();
        if (workspaceId == Guid.Empty) return Unauthorized();

        var document = await _documentRepository
        .GetByIdAsync(documentId, workspaceId);

        if (document is null)
            return NotFound(new { error = "Document not found." });

        var messages = await _documentRepository
        .GetChatHistoryAsync(documentId, limit);

        var response = messages.Select(m => new
        {
            id = m.Id,
            role = m.Role,
            content = m.Content,
            sourceChunks = m.SourceChunksJson is not null
        ? JsonSerializer.Deserialize<object>(m.SourceChunksJson)
        : null,
            createdAt = m.CreatedAt
        });

        return Ok(response);
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

    private static string EscapeForSse(string token)
    {
        return token
        .Replace("\n", "\\n")
        .Replace("\r", "");
    }
}