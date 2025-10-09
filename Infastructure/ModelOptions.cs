using System.ComponentModel.DataAnnotations;

namespace SharpLlama.Infrastructure;

public sealed class ModelOptions
{
    [Required, MinLength(1)]
    public string ModelPath { get; set; } = string.Empty;

    [Range(256, 131072)]
    public int ContextSize { get; set; } = 2048;
}