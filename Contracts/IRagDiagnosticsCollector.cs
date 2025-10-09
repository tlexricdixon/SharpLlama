using Microsoft.KernelMemory;

namespace SharpLlama.Contracts;

public interface IRagDiagnosticsCollector
{
    void AddRetrieval(Guid requestId, string query, IReadOnlyList<Citation> results, int limitUsed);
    IReadOnlyList<RagRetrievalDiagnostics> GetRecent(int max = 20);
}

public sealed record RagRetrievalDiagnostics(
    Guid RequestId,
    DateTimeOffset Timestamp,
    string Query,
    int LimitRequested,
    IReadOnlyList<RagRetrievalResult> Results);

public sealed record RagRetrievalResult(
    string SourceName,
    float MaxRelevance,
    IReadOnlyList<RagRetrievalChunk> Chunks);

public sealed record RagRetrievalChunk(
    string? PartitionId,
    float Relevance,
    string TextPreview);