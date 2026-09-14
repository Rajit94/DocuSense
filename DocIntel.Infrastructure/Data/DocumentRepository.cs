using DocIntel.Core.Entities;
using DocIntel.Core.Enums;
using DocIntel.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Infrastructure.Data;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _context;
   
    public DocumentRepository(AppDbContext context)
    {
        _context = context;
    }
 
    public async Task<Document> CreateAsync(Document document)
    {
      
        await _context.Documents.AddAsync(document);

        await _context.SaveChangesAsync();

        return document;
    }


    public async Task<Document?> GetByIdAsync(Guid id, Guid workspaceId)
    {
        

       
        return await _context.Documents
            .Include(d => d.UploadedBy)   // JOIN to AppUsers table
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    
    public async Task<IEnumerable<Document>> GetAllAsync(Guid workspaceId)
    {
        return await _context.Documents
            .Include(d => d.UploadedBy)
            .OrderByDescending(d => d.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

   
    public async Task UpdateStatusAsync(Guid id, DocumentStatus status)
    {
        await _context.Documents
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.Status, status));
    }

    public async Task SaveChunksAsync(IEnumerable<DocumentChunk> chunks)
    {
       
        await _context.DocumentChunks.AddRangeAsync(chunks);
        await _context.SaveChangesAsync();
    }

   
    public async Task<IEnumerable<DocumentChunk>> GetChunksByDocumentIdAsync(
        Guid documentId)
    {
        return await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)  
            .AsNoTracking()
            .ToListAsync();
    }

   
    public async Task UpdateAnalysisAsync(
        Guid id,
        string summary,
        float riskScore,
        string analysisJson)
    {
        await _context.Documents
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.Summary, summary)
                .SetProperty(d => d.RiskScore, riskScore)
                .SetProperty(d => d.AnalysisJson, analysisJson)
                .SetProperty(d => d.Status, DocumentStatus.Ready));

        
    }

    public async Task DeleteAsync(Guid id, Guid workspaceId)
    {
        
        await _context.Documents
            .Where(d => d.Id == id)
            .ExecuteDeleteAsync();
    }

    
    public async Task<ChatMessage> SaveChatMessageAsync(ChatMessage message)
    {
        await _context.ChatMessages.AddAsync(message);
        await _context.SaveChangesAsync();
        return message;
    }

   
    public async Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(
        Guid documentId,
        int limit = 20)  
    {
        
        return await _context.ChatMessages
            .Where(m => m.DocumentId == documentId)
            .OrderByDescending(m => m.CreatedAt)  
            .Take(limit)                           
            .OrderBy(m => m.CreatedAt)            
            .AsNoTracking()
            .ToListAsync();
    }
}