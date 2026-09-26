using DocIntel.Core.Enums;
using DocIntel.Core.Interfaces;
using DocIntel.Infrastructure.AI;
using DocIntel.Infrastructure.Processing;
using Microsoft.AspNetCore.SignalR;
using DocIntel.API.Hubs;

namespace DocIntel.API.BackgroundJobs;

public class DocumentProcessingJob
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IBlobStorageService _blobStorageService;
    private readonly PdfExtractor _pdfExtractor;
    private readonly DocxExtractor _docxExtractor;
    private readonly TextChunker _textChunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IHubContext<DocumentHub> _hubContext;
    private readonly ILogger<DocumentProcessingJob> _logger;

    public DocumentProcessingJob(
        IDocumentRepository documentRepository,
        IBlobStorageService blobStorageService,
        PdfExtractor pdfExtractor,
        DocxExtractor docxExtractor,
        TextChunker textChunker,
        IEmbeddingService embeddingService,
        IHubContext<DocumentHub> hubContext,
        ILogger<DocumentProcessingJob> logger)
    {
        _documentRepository = documentRepository;
        _blobStorageService = blobStorageService;
        _pdfExtractor = pdfExtractor;
        _docxExtractor = docxExtractor;
        _textChunker = textChunker;
        _embeddingService = embeddingService;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid documentId, Guid workspaceId)
    {
        _logger.LogInformation(
            "Starting processing for document {DocumentId}", documentId);

        try
        {
            // ── STAGE 1: Extracting ───────────────────────────────────────
            await UpdateStatusAsync(documentId, workspaceId,
                DocumentStatus.Extracting);

            var document = await _documentRepository
                .GetByIdAsync(documentId, workspaceId);

            if (document is null)
            {
                _logger.LogError(
                    "Document {DocumentId} not found", documentId);
                return;
            }

            _logger.LogInformation(
                "Extracting text from {FileName}", document.FileName);

            // ── STAGE 2: Embedding ────────────────────────────────────────
            await UpdateStatusAsync(documentId, workspaceId,
                DocumentStatus.Embedding);

            _logger.LogInformation(
                "Embedding chunks for document {DocumentId}", documentId);

            // ── STAGE 3: Analysing ────────────────────────────────────────
            await UpdateStatusAsync(documentId, workspaceId,
                DocumentStatus.Analysing);

            _logger.LogInformation(
                "Analysing document {DocumentId}", documentId);

            // ── STAGE 4: Ready ────────────────────────────────────────────
            await UpdateStatusAsync(documentId, workspaceId,
                DocumentStatus.Ready);

            _logger.LogInformation(
                "Document {DocumentId} processing complete", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process document {DocumentId}", documentId);

            
            await _documentRepository
                .UpdateStatusAsync(documentId, DocumentStatus.Failed);

            await _hubContext.Clients
                .Group(workspaceId.ToString())
                .SendAsync("DocumentStatusChanged", new
                {
                    documentId,
                    status = DocumentStatus.Failed.ToString()
                });
        }
    }


    private async Task UpdateStatusAsync(
        Guid documentId,
        Guid workspaceId,
        DocumentStatus status)
    {
       
        await _documentRepository.UpdateStatusAsync(documentId, status);

        
        await _hubContext.Clients
            .Group(workspaceId.ToString())
            .SendAsync("DocumentStatusChanged", new
            {
                documentId,
                status = status.ToString()
            });
    }
}