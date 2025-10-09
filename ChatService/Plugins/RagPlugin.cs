using Contracts;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ChatService.Plugins;

public class RagPlugin : ISemanticKernelPlugin
{
    private readonly IMemoryService _memoryService;
    private readonly ILoggerManager _logger;
    private readonly IRagDiagnosticsCollector _diag; // added

    public string Name => "RagPlugin";

    public RagPlugin(IMemoryService memoryService, ILoggerManager logger, IRagDiagnosticsCollector? diagnosticsCollector = null)
    {
        _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _diag = diagnosticsCollector ?? new NullRagDiagnosticsCollector();
    }

    [KernelFunction("search_memory")]
    [Description("Search for relevant information in the knowledge base")]
    public async Task<string> SearchMemoryAsync(
        [Description("The search query")] string query,
        [Description("Maximum number of results")] int limit = 5,
        [Description("Minimum relevance score")] double minRelevance = 0.6)
    {
        var opId = Guid.NewGuid().ToString("N");
        var opGuid = Guid.NewGuid(); // new Guid to tie diagnostics
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug($"[RAG:{opId}] Enter SearchMemoryAsync (query='{TrimForLog(query)}', limit={limit}, minRelevance={minRelevance:F2})");

            // Basic validation
            if (string.IsNullOrWhiteSpace(query))
            {
                _logger.LogWarning($"[RAG:{opId}] Empty or null query provided.");
                return "Query was empty. Please provide a search phrase.";
            }

            if (limit <= 0)
            {
                _logger.LogWarning($"[RAG:{opId}] Non-positive limit {limit} provided. Resetting to 5.");
                limit = 5;
            }

            if (double.IsNaN(minRelevance) || minRelevance < 0.0 || minRelevance > 1.0)
            {
                _logger.LogWarning($"[RAG:{opId}] Invalid minRelevance {minRelevance}. Clamping to range 0.0 - 1.0.");
                minRelevance = Math.Clamp(minRelevance, 0.0, 1.0);
            }

            var results = await _memoryService.SearchAsync(query, limit, minRelevance);
            if (results is null)
            {
                _logger.LogWarning($"[RAG:{opId}] IMemoryService returned null result set.");
                return "No relevant information found in the knowledge base.";
            }

            var materialized = results.ToList();
            _logger.LogDebug($"[RAG:{opId}] Retrieved {materialized.Count} raw results.");

            if (materialized.Count == 0)
            {
                return "No relevant information found in the knowledge base.";
            }

            // Diagnostics
            _diag.AddRetrieval(opGuid, query, materialized, limit);

            var sb = new StringBuilder();
            sb.AppendLine("Relevant information from knowledge base:");
            sb.AppendLine();

            int emitted = 0;
            foreach (var result in materialized.Take(limit))
            {
                emitted++;
                var relevance = result.Partitions != null && result.Partitions.Any()
                    ? result.Partitions.Max(p => p.Relevance)
                    : 0.0f;

                var topPartition = result.Partitions != null && result.Partitions.Any()
                    ? result.Partitions.OrderByDescending(p => p.Relevance).First()
                    : null;

                sb.AppendLine($"**Source:** {result.SourceName ?? "Unknown"}");
                sb.AppendLine($"**Relevance:** {relevance:F2}");
                sb.AppendLine($"**Content:** {(topPartition?.Text ?? "(No content available)")}\n");
            }

            _logger.LogInfo($"[RAG:{opId}] Completed search. Returned {emitted} formatted results in {sw.ElapsedMilliseconds} ms.");

            return sb.ToString();
        }
        catch (OperationCanceledException oce)
        {
            _logger.LogWarning($"[RAG:{opId}] Search was canceled: {oce.Message}");
            return "Search was canceled.";
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RAG:{opId}] Error in RAG search: {ex}");
            return "An error occurred while searching the knowledge base.";
        }
        finally
        {
            sw.Stop();
            _logger.LogDebug($"[RAG:{opId}] Exit SearchMemoryAsync (elapsed {sw.ElapsedMilliseconds} ms)");
        }
    }

    public async Task<string> ExecuteAsync(string input, Kernel kernel, KernelArguments? arguments = null)
    {
        var opId = Guid.NewGuid().ToString("N");
        try
        {
            _logger.LogDebug($"[RAG:{opId}] ExecuteAsync invoked (input length={(input ?? string.Empty).Length}).");
            return await SearchMemoryAsync(input);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RAG:{opId}] Unhandled exception in ExecuteAsync: {ex}");
            return "An error occurred while executing the RAG plugin.";
        }
    }

    public async Task<bool> CanHandleAsync(string input)
    {
        try
        {
            // Always true, but log for traceability
            _logger.LogDebug($"[RAG] CanHandleAsync received input length={(input ?? string.Empty).Length} => true");
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RAG] Exception in CanHandleAsync: {ex}");
            // Fail closed (return false) if something unexpected happens
            return false;
        }
    }

    private static string TrimForLog(string? value, int max = 80)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "...";
    }
}