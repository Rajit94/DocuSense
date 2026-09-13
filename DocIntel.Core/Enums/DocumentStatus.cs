namespace DocIntel.Core.Enums;

public enum DocumentStatus
{
    Pending,      // just uploaded, job not started yet
    Extracting,   // pulling text out of PDF/DOCX
    Embedding,    // generating vectors for each chunk
    Analysing,    // GPT-4o generating summary + risk flags
    Ready,        // everything done, user can chat
    Failed        // something went wrong — check logs
}