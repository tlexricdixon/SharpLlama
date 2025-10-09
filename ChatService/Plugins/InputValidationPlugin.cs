using Microsoft.SemanticKernel;
using SharpLlama.Contracts;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ChatService.Plugins;

/// <summary>
/// Plugin that performs basic user input validation and sanitization prior to downstream processing.
/// It enforces length constraints, rejects obviously inappropriate / sensitive content patterns, and strips
/// simple script and URI based injection attempts.
/// </summary>
/// <remarks>
/// Current sanitization is intentionally minimal (regex removal of &lt;script&gt; blocks and simple URI scheme strings).
/// For production scenarios consider:
/// - HTML / Markdown contextual sanitization
/// - More comprehensive profanity / PII detection
/// - Structured security logging of rejected inputs
/// - Incremental / streaming validation for very large inputs
/// </remarks>
public class InputValidationPlugin : IValidationPlugin
{
    /// <summary>
    /// Optional logger (left nullable so existing registrations without logger do not break).
    /// </summary>
    private readonly ILoggerManager? _logger;

    /// <summary>
    /// Gets the plugin name exposed to the Semantic Kernel.
    /// </summary>
    public string Name => "InputValidation";

    /// <summary>
    /// Default constructor (kept for backward compatibility if DI was already set up without logger).
    /// </summary>
    public InputValidationPlugin() { }

    /// <summary>
    /// Preferred constructor allowing logging.
    /// </summary>
    public InputValidationPlugin(ILoggerManager logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates and sanitizes a raw user input string.
    /// </summary>
    /// <param name="input">The user supplied input to validate.</param>
    /// <returns>
    /// A <see cref="ValidationResult"/> indicating:
    /// - <see cref="ValidationResult.IsValid"/>: true if the input passed validation
    /// - <see cref="ValidationResult.ErrorMessage"/>: populated when invalid
    /// - <see cref="ValidationResult.SanitizedInput"/>: sanitized version when valid
    /// </returns>
    /// <remarks>
    /// Validation steps:
    /// 1. Reject null/whitespace.
    /// 2. Enforce maximum length (4000 chars).
    /// 3. Strip simple &lt;script&gt; tags and rudimentary javascript: / data: vectors.
    /// 4. Reject inputs containing disallowed keyword patterns (security / sensitive data terms).
    /// 
    /// This method is asynchronous to satisfy the interface contract and future extensibility,
    /// though it currently performs only CPU-bound synchronous work.
    /// </remarks>
    [KernelFunction, Description("Validates and sanitizes user input for safety")]
    public async Task<ValidationResult> ValidateAsync(
        [Description("The user input to validate")] string input)
    {
        // Method kept async for interface compatibility & future extensibility.
        try
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                _logger?.LogWarning("InputValidationPlugin: empty or whitespace input rejected.");
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Input cannot be empty"
                };
            }

            if (input.Length > 4000)
            {
                _logger?.LogWarning($"InputValidationPlugin: input length {input.Length} exceeds maximum.");
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Input too long (max 4000 characters)"
                };
            }

            // Remove potentially harmful content
            string sanitized = input;
            try
            {
                sanitized = Regex.Replace(sanitized, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase);
                sanitized = sanitized.Replace("javascript:", "", StringComparison.OrdinalIgnoreCase)
                                     .Replace("data:", "", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception rex)
            {
                _logger?.LogError($"InputValidationPlugin: regex sanitization failed: {rex.Message}");
                // Fail closed (treat as invalid rather than passing unsanitized)
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Sanitization error"
                };
            }

            // Check for inappropriate content patterns
            var inappropriatePatterns = new[]
            {
                @"\b(hack|exploit|bypass|malware)\b",
                @"\b(password|credit\s*card|ssn)\b"
            };

            foreach (var pattern in inappropriatePatterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    _logger?.LogWarning($"InputValidationPlugin: rejected input containing pattern '{pattern}'.");
                    return new ValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Input contains inappropriate content"
                    };
                }
            }

            _logger?.LogDebug("InputValidationPlugin: input validated successfully.");
            return new ValidationResult
            {
                IsValid = true,
                SanitizedInput = sanitized
            };
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("InputValidationPlugin: validation canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"InputValidationPlugin: unexpected error {ex.Message}");
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = "Unexpected validation error"
            };
        }
        finally
        {
            await Task.CompletedTask; // preserve async signature with minimal overhead
        }
    }

    /// <summary>
    /// Pass-through execution hook required by the plugin infrastructure.
    /// </summary>
    /// <param name="input">Previously validated input.</param>
    /// <param name="kernel">Semantic Kernel instance (unused).</param>
    /// <param name="arguments">Optional kernel arguments (unused).</param>
    /// <returns>The original input.</returns>
    public Task<string> ExecuteAsync(string input, Kernel kernel, KernelArguments? arguments = null)
    {
        return Task.FromResult(input);
    }

    /// <summary>
    /// Indicates that this validator can always attempt to handle input.
    /// </summary>
    /// <param name="input">The input to test.</param>
    /// <returns>Always returns true.</returns>
    public Task<bool> CanHandleAsync(string input)
    {
        return Task.FromResult(true); // Always can validate
    }
}