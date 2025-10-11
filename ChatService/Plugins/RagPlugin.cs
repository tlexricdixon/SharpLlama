using Microsoft.SemanticKernel;
using SharpLlama.ChatService;
using SharpLlama.Contracts;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ChatService.Plugins;

public class RagPlugin(IKragStore ragStore, ILoggerManager logger, IRagDiagnosticsCollector? diagnosticsCollector = null) : ISemanticKernelPlugin
{
    private readonly IKragStore _ragStore = ragStore ?? throw new ArgumentNullException(nameof(ragStore));
    private readonly ILoggerManager _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IRagDiagnosticsCollector _diag = diagnosticsCollector ?? new NullRagDiagnosticsCollector(); // added

    public string Name => "RagPlugin";

    [KernelFunction("search_memory")]
    [Description("Search for relevant information in the knowledge base")]
    public async Task<string> SearchMemoryAsync(
        [Description("The search query")] string query,
        [Description("Maximum number of results")] int limit = 5,
        [Description("Minimum relevance score")] double minRelevance = 0.6)
    {
        var opId = Guid.NewGuid().ToString("N");
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug($"[RAG:{opId}] Query='{query}' Limit={limit} MinRelevance={minRelevance:F2}");

            if (string.IsNullOrWhiteSpace(query))
                return "Query was empty. Please provide a search phrase.";

            var results = await _ragStore.SearchAsync(query, limit);
            if (results == null || !results.Any())
                return "No relevant information found in the database.";

            var sb = new StringBuilder("Relevant information from company database:\n\n");
            int rank = 1;

            foreach (var chunk in results.Take(limit))
            {
                sb.AppendLine($"**{rank++}. {chunk.TableName}:{chunk.EntityName}**");
                sb.AppendLine(chunk.Text);
                sb.AppendLine();
            }

            _logger.LogInfo($"[RAG:{opId}] Returned {results.Count()} rows in {sw.ElapsedMilliseconds}ms.");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[RAG:{opId}] Exception: {ex}");
            return "An error occurred while querying RAG memory.";
        }
        finally
        {
            sw.Stop();
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