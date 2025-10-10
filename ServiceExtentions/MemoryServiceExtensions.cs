using ChatService;
using LLama;
using LLama.Common;
using LLama.Native;
using LLamaSharp.KernelMemory;
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
                // If later you want to use separate embedding weights with KM, add:
                // memoryBuilder.WithLLamaSharpDefaults(llamaConfig, embeddingWeights);
            }

            return memoryBuilder.Build();
        });

        // FIX: register IMemoryService so RagChatService and EmployeeRagIngestionService can be constructed
        services.AddScoped<IMemoryService, SqlRagMemoryStore>();

        // Optional: IRagChatService is also registered in Program.cs; consider removing one to avoid duplication.
        services.AddScoped<IRagChatService, RagChatService>();

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