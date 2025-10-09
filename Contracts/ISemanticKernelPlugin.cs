using Microsoft.SemanticKernel;

namespace SharpLlama.Contracts;

public interface ISemanticKernelPlugin
{
    string Name { get; }
    Task<string> ExecuteAsync(string input, Kernel kernel, KernelArguments? arguments = null);
    Task<bool> CanHandleAsync(string input);
}

public interface IPreProcessingPlugin : ISemanticKernelPlugin
{
    Task<string> PreProcessAsync(string input, Dictionary<string, object>? context = null);
}

public interface IPostProcessingPlugin : ISemanticKernelPlugin
{
    Task<string> PostProcessAsync(string output, string originalInput, Dictionary<string, object>? context = null);
}

public interface IValidationPlugin : ISemanticKernelPlugin
{
    Task<ValidationResult> ValidateAsync(string input);
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SanitizedInput { get; set; }
}