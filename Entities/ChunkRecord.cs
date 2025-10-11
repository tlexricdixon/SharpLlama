namespace SharpLlama.Entities;

public class ChunkRecord
{
    public string Id { get; set; }
    public string TableName { get; set; }
    public string EntityName { get; set; }
    public string Text { get; set; }
    public float[] Embedding { get; set; }
}
