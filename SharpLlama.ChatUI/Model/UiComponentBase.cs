namespace SharpLlama.ChatUI.CiviTools.Models;

public abstract class UiComponentBase
{
    public Guid Id { get; } = Guid.NewGuid();
    // The Blazor component to render
    public required Type ComponentType { get; init; }

    // Live parameter bag passed to DynamicComponent
    public Dictionary<string, object?> Params { get; } = new(StringComparer.Ordinal);

    // Optional: design metadata hook
    public virtual IEnumerable<PropMeta> GetDesignProps() => Array.Empty<PropMeta>();
}

