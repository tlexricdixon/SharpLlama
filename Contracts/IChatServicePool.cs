using Contracts;
using Entities;
using LLama.Common;

namespace Contracts;

public interface IChatServicePool
{
    Task<IStatefulChatService> GetStatefulServiceAsync(CancellationToken cancellationToken = default);
    Task ReturnStatefulServiceAsync(IStatefulChatService service);
    Task<IStatelessChatService> GetStatelessServiceAsync(CancellationToken cancellationToken = default);
    Task ReturnStatelessServiceAsync(IStatelessChatService service);
    int AvailableStatefulCount { get; }
    int AvailableStatelessCount { get; }
}