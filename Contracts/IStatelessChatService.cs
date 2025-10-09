using LLama.Common;

namespace SharpLlama.Contracts;

public interface IStatelessChatService : IDisposable
{
    Task<string> SendAsync(ChatHistory history, CancellationToken cancellationToken = default);
}