using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace ChatService;

// Adjust names to your real schema.
public sealed class SqliteVectorStore
{
    private readonly string _connectionString;
    private readonly int _expectedDim;
    private readonly bool _vectorsAreNormalized;

    public SqliteVectorStore(
        string connectionString,
        int expectedDimension = 768,
        bool vectorsAreNormalized = false)
    {
        _connectionString = connectionString;
        _expectedDim = expectedDimension;
        _vectorsAreNormalized = vectorsAreNormalized;
    }

    public async Task<IReadOnlyList<VectorHit>> SearchAsync(
        float[] queryEmbedding,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (queryEmbedding.Length != _expectedDim)
            throw new ArgumentException($"Query embedding dim {queryEmbedding.Length} != expected {_expectedDim}");

        // Normalize query if needed (either because DB not normalized, or to ensure cosine is valid)
        if (!_vectorsAreNormalized)
            NormalizeInPlace(queryEmbedding);

        var results = new List<VectorHit>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT id, word, vec, dim FROM embeddings";
        await using var cmd = new SqliteCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0).ToString();
            var word = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (reader.IsDBNull(2)) continue;
            var dim = reader.IsDBNull(3) ? _expectedDim : reader.GetInt32(3);

            if (dim != _expectedDim) continue;

            var blob = (byte[])reader[2];
            if (blob.Length != dim * 4) continue;

            var emb = BlobToFloats(blob);
            if (!_vectorsAreNormalized)
                NormalizeInPlace(emb);

            var score = Cosine(queryEmbedding, emb);
            results.Add(new VectorHit(id, word, score));
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    private static float[] BlobToFloats(byte[] blob)
    {
        var arr = new float[blob.Length / 4];
        Buffer.BlockCopy(blob, 0, arr, 0, blob.Length);
        return arr;
    }

    private static void NormalizeInPlace(float[] v)
    {
        double sum = 0;
        for (int i = 0; i < v.Length; i++) sum += v[i] * v[i];
        var norm = Math.Sqrt(sum);
        if (norm == 0) return;
        var f = (float)(1.0 / norm);
        for (int i = 0; i < v.Length; i++) v[i] *= f;
    }

    private static float Cosine(float[] a, float[] b)
    {
        // If both are normalized, cosine = dot
        double dot = 0;
        for (int i = 0; i < a.Length; i++) dot += a[i] * b[i];
        return (float)dot;
    }
}

public readonly record struct VectorHit(string Id, string Text, float Score);