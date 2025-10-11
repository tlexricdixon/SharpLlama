using SharpLlama.Entities;

namespace SharpLlama.Contracts;

public interface IKragStore
{
    Task UpsertChunkAsync(ChunkRecord chunk);
    Task<IEnumerable<ChunkRecord>> SearchAsync(string query, int topK = 5);
}
