using System;
using System.Collections.Generic;
using System.Text;

namespace DocIntel.Core.Entities
{
    public class DocumentChunk
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid DocumentId { get; set; }

        public Guid WorkspaceId { get; set; }

        public string Text { get; set; } = string.Empty;  // The actual text content of the chunk
        public int PageNumber { get; set; }  // The page number in the original document (1-based)
        public int ChunkIndex { get; set; }  // The index of this chunk in the document (0-based)


        // The Vector embedding - stored as json string for now 
        // will map this to sql server 2025 vector column when we will configure EF core
        public string EmbeddingJson { get; set; } = string.Empty;

        // Navigation property
        public Document Document { get; set; } = null!;
    }

}
