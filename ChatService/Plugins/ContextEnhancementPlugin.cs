using Microsoft.SemanticKernel;
using SharpLlama.Contracts;
using System.ComponentModel;

namespace ChatService.Plugins;

/// <summary>
/// Pre-processing plugin that augments incoming user input with previously cached
/// conversational context if a related entry is found. This helps the downstream
/// model produce more context-aware responses without the caller manually supplying history.
/// </summary>
public class ContextEnhancementPlugin : IPreProcessingPlugin
{
    private readonly IChatResponseCache _cache;
    private readonly ILoggerManager? _logger; // optional to avoid breaking existing registrations

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextEnhancementPlugin"/>.
    /// </summary>
    /// <param name="cache">Cache abstraction used to look up related prior context fragments.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public ContextEnhancementPlugin(IChatResponseCache cache, ILoggerManager? logger = null)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Gets the plugin name used for discovery/registration.
    /// </summary>
    public string Name => "ContextEnhancement";

    /// <summary>
    /// Enhances the provided <paramref name="input"/> by prepending cached related
    /// contextual information when available.
    /// </summary>
    /// <param name="input">Raw user input text to be pre-processed.</param>
    /// <param name="context">
    /// Optional additional context values (not currently used in this simple implementation,
    /// but accepted for future extensibility).
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> whose result is either the original input, or
    /// an augmented string containing retrieved context plus the user input.
    /// </returns>
    [KernelFunction, Description("Enhances input with relevant context")]
    public async Task<string> PreProcessAsync(
        [Description("The user input")] string input,
        Dictionary<string, object>? context = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger?.LogWarning("ContextEnhancementPlugin.PreProcessAsync received empty input.");
            return input;
        }

        try
        {
            var relatedContext = await GetRelatedContextAsync(input);

            if (!string.IsNullOrEmpty(relatedContext))
            {
                _logger?.LogDebug("ContextEnhancementPlugin: related context found and prepended.");
                return $"Context: {relatedContext}\n\nUser: {input}";
            }

            _logger?.LogDebug("ContextEnhancementPlugin: no related context found.");
            return input;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("ContextEnhancementPlugin.PreProcessAsync canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"ContextEnhancementPlugin.PreProcessAsync error: {ex.Message}");
            // Fail open: return original input so pipeline can continue
            return input;
        }
    }

    /// <summary>
    /// Executes the preprocessing logic within a Semantic Kernel invocation pipeline.
    /// </summary>
    /// <param name="input">User input to process.</param>
    /// <param name="kernel">Kernel instance (not used directly here).</param>
    /// <param name="arguments">Optional kernel arguments converted to a dictionary.</param>
    /// <returns>The processed (possibly augmented) input string.</returns>
    public Task<string> ExecuteAsync(string input, Kernel kernel, KernelArguments? arguments = null)
    {
        return PreProcessAsync(input, arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    /// <summary>
    /// Determines whether this plugin should handle the given <paramref name="input"/>.
    /// The heuristic checks for words implying a reference to prior conversation.
    /// </summary>
    /// <param name="input">User input text.</param>
    /// <returns>
    /// True if the input appears to reference earlier discussion; otherwise false.
    /// </returns>
    public Task<bool> CanHandleAsync(string input)
    {
        // Can handle if input seems to reference previous topics
        return Task.FromResult(input.Contains("previous", StringComparison.OrdinalIgnoreCase) ||
                              input.Contains("before", StringComparison.OrdinalIgnoreCase) ||
                              input.Contains("earlier", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Attempts to retrieve a related context snippet from the cache based on the
    /// first token of the input. This is a deliberately simple heuristic.
    /// </summary>
    /// <param name="input">User input used to derive a lookup key.</param>
    /// <returns>
    /// A cached context string if found; otherwise an empty string.
    /// </returns>
    private async Task<string> GetRelatedContextAsync(string input)
    {
        try
        {
            var firstToken = input.Split(' ').FirstOrDefault()?.ToLower();
            if (string.IsNullOrWhiteSpace(firstToken))
            {
                _logger?.LogDebug("ContextEnhancementPlugin: first token empty, skipping cache lookup.");
                return string.Empty;
            }

            var contextKey = _cache.GenerateCacheKey($"context:{firstToken}");
            var cached = await _cache.GetCachedResponseAsync(contextKey) ?? string.Empty;

            if (!string.IsNullOrEmpty(cached))
                _logger?.LogDebug($"ContextEnhancementPlugin: cache hit for key {contextKey}.");
            else
                _logger?.LogDebug($"ContextEnhancementPlugin: cache miss for key {contextKey}.");

            return cached;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"ContextEnhancementPlugin.GetRelatedContextAsync error: {ex.Message}");
            return string.Empty;
        }
    }
}