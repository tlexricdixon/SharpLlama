using LLama.Common;

namespace Contracts;

public interface IStatelessChatService : IDisposable
{
    Task<string> SendAsync(ChatHistory history, CancellationToken cancellationToken = default);
}