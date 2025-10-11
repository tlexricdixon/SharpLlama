namespace SharpLlama.Contracts;

public interface ILocalEmbedder
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}

