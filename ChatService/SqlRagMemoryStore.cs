using Microsoft.Data.SqlClient;
using SharpLlama.Contracts;
using SharpLlama.Entities;
using System.Data;

namespace SharpLlama.ChatService
{
    /// <summary>
    /// SQL-backed RAG memory store that persists text chunks + embeddings
    /// into AI_contextChunks and performs cosine-similarity search.
    /// </summary>
    public class SqlRagMemoryStore : IKragStore
    {
        private readonly string _connectionString;
        private readonly ILocalEmbedder _embed;

        public SqlRagMemoryStore(string connectionString, ILocalEmbedder embed)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _embed = embed ?? throw new ArgumentNullException(nameof(embed));
        }

        /// <summary>
        /// Upsert a chunk and ensure the embedding is stored (computed if missing).
        /// </summary>
        public async Task UpsertChunkAsync(ChunkRecord chunk)
        {
            if (chunk is null) throw new ArgumentNullException(nameof(chunk));
            if (chunk.Embedding == null || chunk.Embedding.Length == 0)
            {
                var text = chunk.Text ?? string.Empty;
                chunk.Embedding = await _embed.EmbedAsync(text);
            }

            const string sql = @"
MERGE AI_contextChunks AS t
USING (SELECT @Id AS Id) AS s ON t.Id = s.Id
WHEN MATCHED THEN UPDATE SET TableName=@TableName, EntityName=@EntityName, ChunkText=@Text, Embedding=@Embedding
WHEN NOT MATCHED THEN INSERT (Id,TableName,EntityName,ChunkText,Embedding)
VALUES (@Id,@TableName,@EntityName,@Text,@Embedding);";

            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);

            cmd.Parameters.AddWithValue("@Id", chunk.Id);
            cmd.Parameters.AddWithValue("@TableName", chunk.TableName ?? string.Empty);
            cmd.Parameters.AddWithValue("@EntityName", chunk.EntityName ?? string.Empty);
            cmd.Parameters.AddWithValue("@Text", chunk.Text ?? string.Empty);
            cmd.Parameters.Add("@Embedding", SqlDbType.VarBinary).Value = ToBytes(chunk.Embedding);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Vector search: embed the query, load candidates, compute cosine, return topK.
        /// </summary>
        public async Task<IEnumerable<ChunkRecord>> SearchAsync(string query, int topK = 5)
        {
            var queryVec = await _embed.EmbedAsync(query ?? string.Empty);
            var all = await LoadAllChunksAsync();

            var ranked = all
                .Select(c => new { c, score = Cosine(queryVec, c.Embedding ?? Array.Empty<float>()) })
                .OrderByDescending(x => x.score)
                .Take(topK)
                .Select(x => x.c)
                .ToList();

            return ranked;
        }

        private async Task<List<ChunkRecord>> LoadAllChunksAsync()
        {
            var list = new List<ChunkRecord>();
            const string sql = "SELECT Id, TableName, EntityName, ChunkText, Embedding FROM AI_contextChunks;";

            using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();
            using var cmd = new SqlCommand(sql, con);
            using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                list.Add(new ChunkRecord
                {
                    Id = rdr.GetString(0),
                    TableName = rdr.GetString(1),
                    EntityName = rdr.GetString(2),
                    Text = rdr.GetString(3),
                    Embedding = rdr.IsDBNull(4) ? Array.Empty<float>() : FromBytes((byte[])rdr[4])
                });
            }
            return list;
        }

        private static double Cosine(float[] a, float[] b)
        {
            int n = Math.Min(a?.Length ?? 0, b?.Length ?? 0);
            if (n == 0) return 0;

            double dot = 0, ma = 0, mb = 0;
            for (int i = 0; i < n; i++)
            {
                dot += a[i] * b[i];
                ma += a[i] * a[i];
                mb += b[i] * b[i];
            }
            return dot / (Math.Sqrt(ma) * Math.Sqrt(mb) + 1e-9);
        }

        private static byte[] ToBytes(float[] v)
        {
            if (v == null || v.Length == 0) return Array.Empty<byte>();
            var bytes = new byte[v.Length * sizeof(float)];
            Buffer.BlockCopy(v, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static float[] FromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return Array.Empty<float>();
            var v = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, v, 0, bytes.Length);
            return v;
        }
    }
}
