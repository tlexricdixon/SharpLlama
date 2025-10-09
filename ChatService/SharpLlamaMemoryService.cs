using Contracts;
using Microsoft.KernelMemory;

namespace ChatService;

public class SharpLlamaMemoryService(IKernelMemory memory, ILoggerManager logger) : IMemoryService
{
    private readonly IKernelMemory _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    private readonly ILoggerManager _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<string> StoreDocumentAsync(string documentId, string content, Dictionary<string, object>? metadata = null)
    {
        try
        {
            var tags = new TagCollection();
            if (metadata != null)
            {
                foreach (var kvp in metadata)
                {
                    tags.Add(kvp.Key, kvp.Value?.ToString() ?? string.Empty);
                }
            }

            await _memory.ImportTextAsync(content, documentId, tags);
            _logger.LogInfo($"Document stored successfully: {documentId}");
            return documentId;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to store document {documentId}: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<Citation>> SearchAsync(string query, int limit = 5, double minRelevance = 0.0)
    {
        try
        {
            var results = await _memory.SearchAsync(query, limit: limit, minRelevance: minRelevance);
            if (results?.Results == null) { _logger.LogWarning("Search returned null results for query: {query}"); return Enumerable.Empty<Citation>(); }
            _logger.LogDebug($"Memory search returned {results.Results.Count} results for query: {query}");
            return results.Results;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to search memory: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        try
        {
            await _memory.DeleteDocumentAsync(documentId);
            _logger.LogInfo($"Document deleted successfully: {documentId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to delete document {documentId}: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetDocumentIdsAsync()
    {
        try
        {
            // This is a simplified implementation - you might need to track document IDs separately
            // or use a different approach based on your KernelMemory configuration
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to get document IDs: {ex.Message}");
            throw;
        }
    }
}