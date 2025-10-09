using System.ComponentModel.DataAnnotations;

namespace SharpLlama.Infrastructure;

public sealed class ChatServiceOptions
{
    // Request / memory timeouts
    [Range(5, 600)]
    public int RequestTimeoutSeconds { get; set; } = 120;

    [Range(1, 120)]
    public int MemorySearchTimeoutSeconds { get; set; } = 30;

    // Inference (centralized)
    [MinLength(1)]
    public string[] AntiPrompts { get; set; } = ["User:"];

    // Soft generation limits / sampling knobs
    [Range(1, 8192)]
    public int MaxTokens { get; set; } = 512;

    // Temperature / TopP ranges depend on model lib (typical defaults)
    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.8;

    [Range(0.0, 1.0)]
    public double TopP { get; set; } = 0.95;

    // Repeat penalty (1 = no penalty)
    [Range(0.5, 5.0)]
    public double RepeatPenalty { get; set; } = 1.0;
}