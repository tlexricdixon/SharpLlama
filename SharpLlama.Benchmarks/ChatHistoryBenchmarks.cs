using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LLama.Common;

namespace SharpLlama.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class ChatHistoryBenchmarks
{
    private ChatHistory _smallHistory = null!;
    private ChatHistory _mediumHistory = null!;
    private ChatHistory _largeHistory = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small history (5 messages)
        _smallHistory = new ChatHistory();
        for (int i = 0; i < 5; i++)
        {
            _smallHistory.AddMessage(AuthorRole.User, $"User message {i}");
            _smallHistory.AddMessage(AuthorRole.Assistant, $"Assistant response {i}");
        }

        // Medium history (50 messages)
        _mediumHistory = new ChatHistory();
        for (int i = 0; i < 25; i++)
        {
            _mediumHistory.AddMessage(AuthorRole.User, $"User message {i} with some additional content to make it longer");
            _mediumHistory.AddMessage(AuthorRole.Assistant, $"Assistant response {i} with detailed explanation and multiple sentences to simulate real conversation flow.");
        }

        // Large history (200 messages)
        _largeHistory = new ChatHistory();
        for (int i = 0; i < 100; i++)
        {
            _largeHistory.AddMessage(AuthorRole.User, $"User message {i} with extensive content that includes detailed questions, context, and background information that would be typical in a long conversation session.");
            _largeHistory.AddMessage(AuthorRole.Assistant, $"Assistant response {i} with comprehensive explanations, examples, code snippets, and detailed analysis that provides thorough answers to complex questions while maintaining context from previous interactions.");
        }
    }

    [Benchmark]
    public ChatHistory CreateSmallHistory()
    {
        var history = new ChatHistory();
        for (int i = 0; i < 5; i++)
        {
            history.AddMessage(AuthorRole.User, $"User message {i}");
            history.AddMessage(AuthorRole.Assistant, $"Assistant response {i}");
        }
        return history;
    }

    [Benchmark]
    public ChatHistory CloneSmallHistory()
    {
        var newHistory = new ChatHistory();
        newHistory.Messages.AddRange(_smallHistory.Messages);
        return newHistory;
    }

    [Benchmark]
    public ChatHistory CloneMediumHistory()
    {
        var newHistory = new ChatHistory();
        newHistory.Messages.AddRange(_mediumHistory.Messages);
        return newHistory;
    }

    [Benchmark]
    public ChatHistory CloneLargeHistory()
    {
        var newHistory = new ChatHistory();
        newHistory.Messages.AddRange(_largeHistory.Messages);
        return newHistory;
    }

    [Benchmark]
    public int CountTokensInSmallHistory()
    {
        return _smallHistory.Messages.Sum(m => m.Content.Length);
    }

    [Benchmark]
    public int CountTokensInMediumHistory()
    {
        return _mediumHistory.Messages.Sum(m => m.Content.Length);
    }

    [Benchmark]
    public int CountTokensInLargeHistory()
    {
        return _largeHistory.Messages.Sum(m => m.Content.Length);
    }

    [Benchmark]
    public ChatHistory.Message[] FilterUserMessages()
    {
        return _mediumHistory.Messages.Where(m => m.AuthorRole == AuthorRole.User).ToArray();
    }

    [Benchmark]
    public ChatHistory.Message[] FilterAssistantMessages()
    {
        return _mediumHistory.Messages.Where(m => m.AuthorRole == AuthorRole.Assistant).ToArray();
    }
}