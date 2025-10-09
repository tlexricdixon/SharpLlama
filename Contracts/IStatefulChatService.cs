using Entities;
using SharpLlama.Entities;
using System.Runtime.CompilerServices;

namespace SharpLlama.Contracts
{
    public interface IStatefulChatService
    {
        void Dispose();
        Task<string> Send(SendMessageInput input);

        // Streaming with cancellation support (client disconnect / abort).
        IAsyncEnumerable<string> SendStream(
            SendMessageInput input,
            [EnumeratorCancellation] CancellationToken cancellationToken = default);
    }
}