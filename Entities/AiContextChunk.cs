namespace SharpLlama.Entities;

public partial class AiContextChunk
{
    public int ChunkId { get; set; }

    public string? SourceTable { get; set; }

    public string? SourceKey { get; set; }

    public string? ChunkText { get; set; }

    public byte[]? Embedding { get; set; }
}
