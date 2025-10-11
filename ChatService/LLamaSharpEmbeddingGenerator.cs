using LLama;
using LLama.Common;          // ModelParams (0.25.0) implements the required interfaces
using SharpLlama.Contracts;

namespace SharpLlama.ChatService;

/// <summary>
/// Local embedder that uses a GGUF embedding model with LlamaSharp 0.25.0.
/// </summary>
public sealed class LLamaSharpLocalEmbedder : ILocalEmbedder, IDisposable
{
    private readonly LLamaEmbedder _embedder;
    private readonly LLamaWeights _weights;

    /// <param name="modelPath">Path to a GGUF embedding model (e.g., nomic-embed-text-v1.5.Q2_K.gguf)</param>
    /// <param name="contextSize">Context tokens for the embedding context</param>
    /// <param name="gpuLayers">Number of GPU layers (0 = CPU)</param>
    public LLamaSharpLocalEmbedder(string modelPath, uint contextSize = 512, int gpuLayers = 0)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path is required.", nameof(modelPath));
        if (!System.IO.File.Exists(modelPath))
            throw new System.IO.FileNotFoundException("Embedding model not found", modelPath);

        // One params instance is used both to load weights and as context params
        var mparams = new ModelParams(modelPath)
        {
            Embeddings = true,
            ContextSize = contextSize,
            GpuLayerCount = gpuLayers
        };

        // LlamaSharp 0.25.0: LoadFromFile expects IModelParams (ModelParams implements it)
        _weights = LLamaWeights.LoadFromFile(mparams);

        // LlamaSharp 0.25.0: LLamaEmbedder ctor is (LLamaWeights, IContextParams)
        _embedder = new LLamaEmbedder(_weights, mparams);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (text is null) text = string.Empty;

        // LlamaSharp 0.25.0: GetEmbeddings returns Task<IReadOnlyList<float[]>>
        var vectors = await _embedder.GetEmbeddings(text);

        if (vectors != null && vectors.Count > 0)
            return vectors[0];

        return Array.Empty<float>();
    }

    public void Dispose()
    {
        _embedder?.Dispose();
        _weights?.Dispose();
    }
}
