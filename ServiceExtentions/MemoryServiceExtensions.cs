using ChatService.Plugins;
using LLama;
using LLama.Common;
using LLama.Native;
using LLamaSharp.KernelMemory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using SharpLlama.ChatService;
using SharpLlama.Contracts;

namespace SharpLlama.ServiceExtentions;

public static class MemoryServiceExtensions
{
    public static IServiceCollection AddMemoryServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IKernelMemory>(serviceProvider =>
        {
            var mainModelRaw = configuration["ModelPath"] ?? throw new InvalidOperationException("ModelPath is required");
            var embeddingModelRaw = configuration["EmbeddingModelPath"]; // may be blank to force fallback

            var mainModelPath = ResolveModelPath(mainModelRaw);
            var embeddingModelPath = !string.IsNullOrWhiteSpace(embeddingModelRaw)
                ? ResolveModelPath(embeddingModelRaw)
                : mainModelPath; // use main model for embeddings if none specified

            if (!File.Exists(mainModelPath))
                throw new FileNotFoundException($"LLama main model file not found: {mainModelPath}", mainModelPath);
            if (!File.Exists(embeddingModelPath))
                throw new FileNotFoundException($"Embedding model file not found: {embeddingModelPath}", embeddingModelPath);

            var weightManager = serviceProvider.GetRequiredService<ILLamaWeightManager>();
            var logger = serviceProvider.GetService<ILoggerManager>(); // optional logging

            // Generation (and embeddings if same file) params
            var genParams = new ModelParams(mainModelPath)
            {
                ContextSize = (uint?)(int.TryParse(configuration["ContextSize"], out var ctx) ? ctx : 2048),
            };
            if (int.TryParse(configuration["LLama:MainGpu"], out var mainGpu))
                genParams.MainGpu = mainGpu;
            if (int.TryParse(configuration["LLama:GpuLayerCount"], out var gpuLayers) && gpuLayers >= 0)
                genParams.GpuLayerCount = gpuLayers;
            if (bool.TryParse(configuration["LLama:FlashAttention"], out var flash))
                genParams.FlashAttention = flash;
            var splitModeStr = configuration["LLama:SplitMode"];
            if (!string.IsNullOrWhiteSpace(splitModeStr) &&
                Enum.TryParse<GPUSplitMode>(splitModeStr, true, out var splitMode))
                genParams.SplitMode = splitMode;

            var mainWeights = weightManager.GetOrCreateWeights(mainModelPath, genParams);

            // We intentionally avoid a second embedding weights load unless a distinct embedding model is specified.
            LLamaWeights? embeddingWeights = null;
            if (!string.Equals(mainModelPath, embeddingModelPath, StringComparison.OrdinalIgnoreCase))
            {
                var embParams = new ModelParams(embeddingModelPath)
                {
                    ContextSize = 512,
                    Embeddings = true,
                    GpuLayerCount = 0
                };
                embeddingWeights = weightManager.GetOrCreateWeights(embeddingModelPath, embParams);
            }

            var llamaConfig = new LLamaSharpConfig(mainModelPath)
            {
                ContextSize = 2048
            };

            var memoryBuilder = new KernelMemoryBuilder()
                .WithLLamaSharpDefaults(llamaConfig, mainWeights); // single registration

            // Storage configuration
            var storageType = configuration["Memory:StorageType"] ?? "Volatile";
            if (storageType.Equals("disk", StringComparison.OrdinalIgnoreCase))
            {
                // Absolute path to avoid surprises with working directory differences
                var configuredDataPath = configuration["Memory:DataPath"] ?? "memory_data";
                var dataPath = Path.GetFullPath(Path.IsPathRooted(configuredDataPath)
                    ? configuredDataPath
                    : Path.Combine(AppContext.BaseDirectory, configuredDataPath));

                Directory.CreateDirectory(dataPath);

                // Persistent document store
                memoryBuilder.WithSimpleFileStorage(dataPath);

                // Persistent vector store (subfolder)
                var vectorDir = Path.Combine(dataPath, "vector_index");
                Directory.CreateDirectory(vectorDir);
                memoryBuilder.WithSimpleVectorDb(vectorDir);

                logger?.LogInfo($"KernelMemory: Using DISK storage. DataPath={dataPath} VectorDir={vectorDir}");
            }
            else
            {
                memoryBuilder.WithSimpleVectorDb(); // purely in-memory (documents + vectors)
                logger?.LogInfo("KernelMemory: Using VOLATILE in-memory storage.");
            }

            // (Optional) If a distinct embedding model was loaded but we skipped adding it, log that fact.
            if (embeddingWeights != null)
            {
                logger?.LogInfo("KernelMemory: Separate embedding model loaded (distinct file).");
                // To use separate embedding weights with KernelMemory in the future,
                // add a second LLamaSharp defaults registration configured for embeddings.
            }

            return memoryBuilder.Build();
        });
        services.AddScoped<IRagChatService, RagChatService>();
        services.AddScoped<ISemanticKernelPlugin, RagPlugin>();
        services.AddScoped<IRagDiagnosticsCollector, RagDiagnosticsCollector>();
        services.AddScoped<IEmployeeRagIngestionService, EmployeeRagIngestionService>();
        services.AddScoped<StructuredEmployeeQueryService>();

        return services;
    }
    public static IServiceCollection AddSqlRagWithLocalEmbeddings(this IServiceCollection services, IConfiguration config)
    {
        // Local embedder: look for model in the SharpLlama project's Models folder
        services.AddSingleton<ILocalEmbedder>(sp =>
        {
            var fileName = config["EmbeddingModelFileName"] ?? "nomic-embed-text-v1.5.Q2_K.gguf";

            // 1) Allow explicit override via configuration (EmbeddingModelPath)
            var explicitPath = config["EmbeddingModelPath"];
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                var resolvedExplicit = ResolveModelPath(explicitPath);
                if (!File.Exists(resolvedExplicit))
                    throw new FileNotFoundException("Embedding model not found (EmbeddingModelPath).", resolvedExplicit);

                return new LLamaSharpLocalEmbedder(resolvedExplicit, contextSize: 512, gpuLayers: 0);
            }

            // 2) Preferred: SharpLlama/Models/<file>
            var candidate1 = ResolveModelPath(Path.Combine("SharpLlama", "Models", fileName));

            // 3) Fallbacks: ./Models and BaseDirectory/Models
            var candidate2 = ResolveModelPath(Path.Combine("Models", fileName));
            var candidate3 = Path.Combine(AppContext.BaseDirectory, "Models", fileName);

            string? modelPath = null;
            if (File.Exists(candidate1)) modelPath = candidate1;
            else if (File.Exists(candidate2)) modelPath = candidate2;
            else if (File.Exists(candidate3)) modelPath = candidate3;

            if (modelPath is null)
            {
                var attempts = new[]
                {
                    candidate1,
                    candidate2,
                    candidate3
                };
                throw new FileNotFoundException(
                    "Embedding model not found in expected locations (SharpLlama/Models or ./Models).",
                    string.Join(" | ", attempts));
            }

            // Adjust gpuLayers if CUDA build available
            return new LLamaSharpLocalEmbedder(modelPath, contextSize: 512, gpuLayers: 0);
        });

        // SQL RAG store
        services.AddScoped<IKragStore>(sp =>
        {
            var connStr = config.GetConnectionString("DefaultConnection")
                          ?? config["ConnectionStrings:DefaultConnection"]
                          ?? throw new InvalidOperationException("Database connection string not found.");
            var embedder = sp.GetRequiredService<ILocalEmbedder>();
            return new SqlRagMemoryStore(connStr, embedder);
        });

        return services;
    }
    private static string ResolveModelPath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;
        if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        var baseDir = AppContext.BaseDirectory;

        var attempt1 = Path.GetFullPath(Path.Combine(baseDir, configuredPath));
        if (File.Exists(attempt1)) return attempt1;

        var attempt2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", configuredPath));
        if (File.Exists(attempt2)) return attempt2;

        return Path.GetFullPath(configuredPath);
    }
}