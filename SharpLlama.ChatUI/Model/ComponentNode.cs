namespace SharpLlama.ChatUI.CiviTools.Models;

public class ComponentNode(UiComponentBase component)
{
    public UiComponentBase Component { get; set; } = component;
    public List<ComponentNode> Children { get; set; } = new();
}

// If you like keeping Getter/Setter, make them act on the Params bag
public record PropMeta(
    string Name,
    string Label,
    string Type, // "string", "int", "bool", "int", "date", "list"
    Func<Dictionary<string, object?>, object?> Getter,
    Action<Dictionary<string, object?>, object?> Setter,
    IEnumerable<(string Value, string Label)>? Options = null
);
