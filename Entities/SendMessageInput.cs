using System.ComponentModel.DataAnnotations;

namespace SharpLlama.Entities;

public sealed class SendMessageInput
{
    [Required]
    [StringLength(8000, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;
}


