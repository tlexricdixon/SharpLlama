using LLama.Common;

namespace SharpLlama.Contracts;

public interface IRagChatService : IStatelessChatService
{
    Task<string> SendWithRagAsync(ChatHistory history, string? collectionName = null, CancellationToken cancellationToken = default);
    //Task<bool> AddDocumentAsync(string documentId, string content, Dictionary<string, object>? metadata = null);
    //Task<bool> DeleteDocumentAsync(string documentId);
}