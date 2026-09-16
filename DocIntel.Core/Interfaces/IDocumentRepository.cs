using DocIntel.Core.Entities;
using DocIntel.Core.Enums;

namespace DocIntel.Core.Interfaces;

public interface IDocumentRepository
{
    Task<Document> CreateAsync(Document document);
    Task<Document?> GetByIdAsync(Guid id, Guid workspaceId);
    Task<IEnumerable<Document>> GetAllAsync(Guid workspaceId);
    Task UpdateStatusAsync(Guid id, DocumentStatus status);
    Task UpdateAnalysisAsync(Guid id, string summary, float riskScore, string analysisJson);
    Task SaveChunksAsync(IEnumerable<DocumentChunk> chunks);
    Task<IEnumerable<DocumentChunk>> GetChunksByDocumentIdAsync(Guid documentId);
    Task<ChatMessage> SaveChatMessageAsync(ChatMessage message);
    Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(Guid documentId, int limit = 20);
    Task DeleteAsync(Guid id, Guid workspaceId);
}