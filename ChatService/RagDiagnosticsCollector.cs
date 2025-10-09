using Microsoft.KernelMemory;
using SharpLlama.Contracts;

namespace SharpLlama.ChatService;

public sealed class RagDiagnosticsCollector : IRagDiagnosticsCollector
{
    private readonly int _capacity;
    private readonly LinkedList<RagRetrievalDiagnostics> _buffer = new();
    private readonly object _lock = new();

    public RagDiagnosticsCollector(int capacity = 100)
    {
        _capacity = capacity <= 0 ? 100 : capacity;
    }

    public void AddRetrieval(Guid requestId, string query, IReadOnlyList<Citation> results, int limitUsed)
    {
        var materialized = new List<RagRetrievalResult>(results.Count);

        foreach (var r in results)
        {
            var partitions = r.Partitions ?? new List<Citation.Partition>();
            var chunkModels = partitions
                .OrderByDescending(p => p.Relevance)
                .Select(p => new RagRetrievalChunk(
                    p.PartitionNumber.ToString(), // Remove the '?' operator
                    p.Relevance,
                    BuildPreview(p.Text)))
                .ToList();

            float maxRel = partitions.Count == 0 ? 0f : partitions.Max(p => p.Relevance);
            materialized.Add(new RagRetrievalResult(r.SourceName ?? "Unknown", maxRel, chunkModels));
        }

        var diag = new RagRetrievalDiagnostics(
            requestId,
            DateTimeOffset.UtcNow,
            query,
            limitUsed,
            materialized);

        lock (_lock)
        {
            _buffer.AddFirst(diag);
            while (_buffer.Count > _capacity)
                _buffer.RemoveLast();
        }
    }

    public IReadOnlyList<RagRetrievalDiagnostics> GetRecent(int max = 20)
    {
        lock (_lock)
        {
            return _buffer.Take(max).ToList();
        }
    }

    private static string BuildPreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        var trimmed = text.Trim();
        return trimmed.Length <= 240 ? trimmed : trimmed[..240] + "...";
    }
}

public sealed class NullRagDiagnosticsCollector : IRagDiagnosticsCollector
{
    public void AddRetrieval(Guid requestId, string query, IReadOnlyList<Citation> results, int limitUsed) { }
    public IReadOnlyList<RagRetrievalDiagnostics> GetRecent(int max = 20) => Array.Empty<RagRetrievalDiagnostics>();
}