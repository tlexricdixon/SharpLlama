namespace SharpLlama.ChatUI.Services;

public sealed class ChatMessage
{
    public required string Role { get; init; }   // "user" or "assistant"
    public required string Content { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ChatState
{
    private readonly List<ChatMessage> _messages = new();
    public IReadOnlyList<ChatMessage> Messages => _messages;

    public void Add(string role, string content) =>
        _messages.Add(new ChatMessage { Role = role, Content = content });

    public void Clear() => _messages.Clear();
}