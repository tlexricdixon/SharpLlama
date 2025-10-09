using System.ComponentModel.DataAnnotations;

namespace Entities;

public sealed class HistoryInput
{
    [Required]
    [MinLength(1)]
    [MaxLength(64)]
    public List<HistoryItem> Messages { get; set; } = [];

    public sealed class HistoryItem
    {
        [Required]
        [StringLength(32)]
        public string Role { get; set; } = string.Empty;

        [StringLength(8000)]
        public string? Content { get; set; }
    }
}
