using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LLama.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpLlama.ChatService;
using SharpLlama.Contracts;
using SharpLlama.Entities;

namespace SharpLlama.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[MarkdownExporter, AsciiDocExporter, HtmlExporter]
public class ChatServiceBenchmarks
{
    private IStatefulChatService _statefulChatService = null!;
    private IStatelessChatService _statelessChatService = null!;
    private ILoggerManager _logger = null!;
    private IConfiguration _configuration = null!;
    private ServiceProvider _serviceProvider = null!;

    private SendMessageInput _shortMessage = null!;
    private SendMessageInput _mediumMessage = null!;
    private SendMessageInput _longMessage = null!;
    private HistoryInput _historyInput = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Configure services
        var services = new ServiceCollection();

        // Add configuration
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables();

        _configuration = configBuilder.Build();
        services.AddSingleton(_configuration);

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add mock logger manager for benchmarking
        services.AddSingleton<ILoggerManager, MockLoggerManager>();

        // Add chat services
        services.AddTransient<IStatefulChatService, StatefulChatService>();
        services.AddTransient<IStatelessChatService, StatelessChatService>();

        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILoggerManager>();
        _statefulChatService = _serviceProvider.GetRequiredService<IStatefulChatService>();
        _statelessChatService = _serviceProvider.GetRequiredService<IStatelessChatService>();

        // Setup test data
        _shortMessage = new SendMessageInput { Text = "Hello" };
        _mediumMessage = new SendMessageInput { Text = "Can you explain what machine learning is in simple terms?" };
        _longMessage = new SendMessageInput { Text = "I need a detailed explanation of how neural networks work, including the mathematical foundations, backpropagation algorithm, and various architectures like CNNs and RNNs. Please provide examples and use cases for each type." };

        _historyInput = new HistoryInput
        {
            Messages = new List<HistoryInput.HistoryItem>
            {
                new() { Role = "User", Content = "Hello, how are you?" },
                new() { Role = "Assistant", Content = "I'm doing well, thank you!" },
                new() { Role = "User", Content = "Can you help me with programming?" }
            }
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _statefulChatService?.Dispose();
        _serviceProvider?.Dispose();
    }

    [Benchmark]
    public async Task<string> StatefulChatService_ShortMessage()
    {
        return await _statefulChatService.Send(_shortMessage);
    }

    [Benchmark]
    public async Task<string> StatefulChatService_MediumMessage()
    {
        return await _statefulChatService.Send(_mediumMessage);
    }

    [Benchmark]
    public async Task<string> StatefulChatService_LongMessage()
    {
        return await _statefulChatService.Send(_longMessage);
    }

    [Benchmark]
    public async Task<string> StatelessChatService_History()
    {
        var history = new ChatHistory();
        var messages = _historyInput.Messages.Select(m =>
            new ChatHistory.Message(Enum.Parse<AuthorRole>(m.Role), m.Content));
        history.Messages.AddRange(messages);

        return await _statelessChatService.SendAsync(history);
    }

    [Benchmark]
    public async Task StatefulChatService_StreamShortMessage()
    {
        await foreach (var _ in _statefulChatService.SendStream(_shortMessage))
        {
            // Consume the stream
        }
    }

    [Benchmark]
    public async Task StatefulChatService_StreamMediumMessage()
    {
        await foreach (var _ in _statefulChatService.SendStream(_mediumMessage))
        {
            // Consume the stream
        }
    }
}

// Mock logger for benchmarking to avoid I/O overhead
public class MockLoggerManager : ILoggerManager
{
    public void LogDebug(string message) { }
    public void LogError(string message) { }
    public void LogInfo(string message) { }
    public void LogInfo(string message, string systemPrompt) { }

    public void LogInfo(string message, params object[] args)
    {
        throw new NotImplementedException();
    }

    public void LogWarning(string message) { }
}