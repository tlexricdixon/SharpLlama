using Contracts;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ChatService.Plugins;

/// <summary>
/// Post-processing plugin that formats and refines raw AI model responses to improve readability,
/// consistency, and usefulness before they are returned to the caller.
/// </summary>
/// <remarks>
/// Processing steps:
/// 1. Removes redundant lead-in phrases (e.g., "I think that").
/// 2. Ensures the first character is capitalized if appropriate.
/// 3. Appends a clarifying prompt if the response appears too short to be useful.
/// 4. Ensures the response ends with terminal punctuation (., !, or ?).
/// This plugin always reports it can handle any input.
/// </remarks>
public class ResponseFormattingPlugin : IPostProcessingPlugin
{
    private readonly ILogger<ResponseFormattingPlugin> _logger;

    /// <summary>
    /// Creates a new instance of <see cref="ResponseFormattingPlugin"/>.
    /// </summary>
    /// <param name="logger">Optional logger. If null, a no-op logger is used.</param>
    public ResponseFormattingPlugin(ILogger<ResponseFormattingPlugin>? logger = null)
    {
        _logger = logger ?? NullLogger<ResponseFormattingPlugin>.Instance;
    }

    /// <summary>
    /// Gets the unique plugin name used for discovery/registration.
    /// </summary>
    public string Name => "ResponseFormatting";

    /// <summary>
    /// Applies a series of formatting heuristics to the raw AI output to improve clarity and completeness.
    /// </summary>
    /// <param name="output">The raw AI generated response prior to formatting.</param>
    /// <param name="originalInput">The original user input that produced the AI response (not currently used, but available for future logic).</param>
    /// <param name="context">Optional contextual values supplied by the caller or pipeline.</param>
    /// <returns>A formatted response string with standardized tone and punctuation.</returns>
    [KernelFunction, Description("Formats and improves the AI response")]
    public Task<string> PostProcessAsync(
        [Description("The AI response")] string output,
        [Description("The original user input")] string originalInput,
        Dictionary<string, object>? context = null)
    {
        // Defensive normalization
        output ??= string.Empty;
        originalInput ??= string.Empty;

        try
        {
            _logger.LogDebug("Starting post-processing. RawLength={Length} OriginalInputLength={OriginalLength}", output.Length, originalInput.Length);

            var formatted = output;

            // Remove redundant phrases
            var before = formatted;
            formatted = Regex.Replace(formatted,
                @"\b(I think that|I believe that|It seems that)\s*",
                "",
                RegexOptions.IgnoreCase);
            if (!ReferenceEquals(before, formatted) && before != formatted)
            {
                _logger.LogTrace("Removed redundant lead-in phrase. Before='{BeforeSnippet}' After='{AfterSnippet}'",
                    Truncate(before), Truncate(formatted));
            }

            // Ensure proper capitalization
            if (!string.IsNullOrEmpty(formatted) && char.IsLower(formatted[0]))
            {
                var oldFirst = formatted[0];
                formatted = char.ToUpper(formatted[0]) + formatted[1..];
                _logger.LogTrace("Capitalized first character from '{Old}' to '{New}'", oldFirst, formatted[0]);
            }

            // Add helpful context if response seems incomplete
            if (formatted.Length < 10)
            {
                formatted += " Could you provide more details about what you're looking for?";
                _logger.LogTrace("Appended clarification due to short response. NewLength={Length}", formatted.Length);
            }

            // Ensure response ends with proper punctuation
            if (!string.IsNullOrEmpty(formatted) && !".!?".Contains(formatted[^1]))
            {
                formatted += ".";
                _logger.LogTrace("Appended terminal punctuation. FinalChar='{Char}'", formatted[^1]);
            }

            _logger.LogDebug("Post-processing complete. FinalLength={Length}", formatted.Length);
            return Task.FromResult(formatted);
        }
        catch (RegexMatchTimeoutException rex)
        {
            _logger.LogError(rex, "Regex timeout while formatting response. Returning original output.");
            return Task.FromResult(output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during post-processing. Returning original output.");
            return Task.FromResult(output);
        }
    }

    /// <summary>
    /// Executes the post-processing workflow by delegating to <see cref="PostProcessAsync"/> using the provided arguments.
    /// </summary>
    /// <param name="input">The raw AI output to format.</param>
    /// <param name="kernel">Kernel instance (not used directly here, but required by the interface).</param>
    /// <param name="arguments">Optional argument collection; if present, attempts to extract the original input via key "originalInput".</param>
    /// <returns>The formatted response.</returns>
    public async Task<string> ExecuteAsync(string input, Kernel kernel, KernelArguments? arguments = null)
    {
        try
        {
            var originalInput = arguments?["originalInput"]?.ToString() ?? string.Empty;
            _logger.LogDebug("ExecuteAsync invoked. InputLength={InputLength} OriginalInputLength={OriginalInputLength}", input?.Length ?? 0, originalInput.Length);
            var dict = arguments?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return await PostProcessAsync(input, originalInput, dict).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAsync. Returning unmodified input.");
            return input;
        }
    }

    /// <summary>
    /// Indicates whether this plugin can handle the given input. Always returns <c>true</c>.
    /// </summary>
    /// <param name="input">The candidate content to evaluate.</param>
    /// <returns><c>true</c> in all cases.</returns>
    public Task<bool> CanHandleAsync(string input)
    {
        // Lightweight enough that it always opts in.
        return Task.FromResult(true);
    }

    private static string Truncate(string value, int max = 60)
        => string.IsNullOrEmpty(value) ? value : (value.Length <= max ? value : value[..max] + "...");
}