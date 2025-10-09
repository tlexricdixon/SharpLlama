using Microsoft.KernelMemory;

namespace SharpLlama.Contracts;

public interface IMemoryService
{
    Task<string> StoreDocumentAsync(string documentId, string content, Dictionary<string, object>? metadata = null);
    Task<IEnumerable<Citation>> SearchAsync(string query, int limit = 5, double minRelevance = 0.0);
    Task<bool> DeleteDocumentAsync(string documentId);
    Task<IEnumerable<string>> GetDocumentIdsAsync();
}