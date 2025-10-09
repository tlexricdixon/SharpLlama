using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.Configuration;

namespace SharpLlama.Benchmarks;

[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class LlamaContextBenchmarks
{
    private LLamaWeights _weights = null!;
    private LLamaContext _context = null!;
    private ModelParams _params = null!;
    private IConfiguration _configuration = null!;
    private readonly Dictionary<int, LLamaToken[]> _detokenizeTokenCache = new();

    [GlobalSetup]
    public void Setup()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        _configuration = configBuilder.Build();

        var modelPath = _configuration["ModelPath"];
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new InvalidOperationException(
                "ModelPath configuration value is missing. Provide it in appsettings.json or environment variables (e.g., ModelPath=/path/to/model).");
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found at path '{modelPath}'. Set a valid path in configuration.", modelPath);
        }

        _params = new ModelParams(modelPath)
        {
            ContextSize = 512,
        };

        _weights = LLamaWeights.LoadFromFile(_params);
        _context = new LLamaContext(_weights, _params);

        foreach (var size in new[] { 10, 50, 100 })
        {
            var tokens = new LLamaToken[size];
            for (int i = 0; i < size; i++)
            {
                // LLamaToken constructor is not publicly accessible; use explicit cast operator instead.
                tokens[i] = (LLamaToken)1;
            }
            _detokenizeTokenCache[size] = tokens;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context?.Dispose();
        _weights?.Dispose();
    }

    [Benchmark]
    public void CreateContext()
    {
        using var context = new LLamaContext(_weights, _params);
    }

    [Benchmark]
    [Arguments("Hello world")]
    [Arguments("This is a medium length message for testing")]
    [Arguments("This is a much longer message that contains more tokens and should take more time to process through the language model context")]
    public LLamaToken[] TokenizeText(string text)
    {
        return _context.Tokenize(text);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public string DetokenizeTokens(int tokenCount)
    {
        var tokens = _detokenizeTokenCache[tokenCount];
        return _context.DeTokenize(tokens);
    }
}