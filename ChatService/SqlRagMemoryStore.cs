using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using SharpLlama.Contracts;
using SharpLlama.Entities;

namespace SharpLlama.ChatService;

// Simple SQL-backed memory service that retrieves chunks from AI_ContextChunks.
public sealed class SqlRagMemoryStore : IMemoryService
{
    private readonly NorthwindStarterContext _db;

    public SqlRagMemoryStore(NorthwindStarterContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IEnumerable<Citation>> SearchAsync(string query, int limit = 5, double minRelevance = 0.0)
    {
        limit = limit <= 0 ? 5 : limit;
        minRelevance = double.IsNaN(minRelevance) ? 0.0 : Math.Clamp(minRelevance, 0.0, 1.0);

        var tokens = Tokenize(query);
        if (tokens.Count == 0)
            return Array.Empty<Citation>();

        // Filter rows that match ANY token (translated as OR in SQL), cap prefetch for scoring.
        var prefetch = await _db.AiContextChunks.AsNoTracking()
            .Where(c => c.ChunkText != null && tokens.Any(t => EF.Functions.Like(c.ChunkText!, $"%{t}%")))
            .Select(c => new { c.ChunkId, c.SourceTable, c.SourceKey, c.ChunkText })
            .Take(200)
            .ToListAsync()
            .ConfigureAwait(false);

        // Score rows by fraction of tokens matched (0..1), then filter by minRelevance.
        var ranked = prefetch
            .Select(c =>
            {
                int hits = tokens.Count(t => c.ChunkText!.Contains(t, StringComparison.OrdinalIgnoreCase));
                double score = tokens.Count == 0 ? 0 : (double)hits / tokens.Count;
                return (c, score);
            })
            .Where(x => x.score >= minRelevance)
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => (x.c.ChunkText?.Length ?? 0))
            .Take(limit)
            .ToList();

        var citations = new List<Citation>(ranked.Count);
        foreach (var (c, score) in ranked)
        {
            var partition = new Citation.Partition
            {
                PartitionNumber = c.ChunkId,
                Relevance = (float)score,
                Text = c.ChunkText ?? string.Empty
            };

            citations.Add(new Citation
            {
                SourceName = $"{c.SourceTable ?? "Unknown"}:{c.SourceKey ?? "Unknown"}",
                Partitions = new List<Citation.Partition> { partition }
            });
        }

        return citations;
    }

    public async Task<string> StoreDocumentAsync(string documentId, string content, Dictionary<string, object>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(documentId)) throw new ArgumentException("documentId is required.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("content is required.", nameof(content));

        var row = new AiContextChunk
        {
            SourceTable = metadata != null && metadata.TryGetValue("SourceTable", out var st) ? Convert.ToString(st) : "Documents",
            SourceKey = documentId,
            ChunkText = content,
            Embedding = null
        };

        _db.AiContextChunks.Add(row);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        return documentId;
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId)) return false;

        var rows = await _db.AiContextChunks
            .Where(c => c.SourceKey == documentId)
            .ToListAsync()
            .ConfigureAwait(false);

        if (rows.Count == 0) return false;

        _db.AiContextChunks.RemoveRange(rows);
        await _db.SaveChangesAsync().ConfigureAwait(false);
        return true;
    }

    public async Task<IEnumerable<string>> GetDocumentIdsAsync()
    {
        return await _db.AiContextChunks.AsNoTracking()
            .Where(c => c.SourceKey != null)
            .Select(c => c.SourceKey!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return [.. text.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '-', '_' },
                          StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Where(t => t.Length > 2)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .Take(12)];
    }
}

