using SharpLlama.ChatUI.Components.UI;

namespace SharpLlama.ChatUI.CiviTools.Models;

//public class UiTextField : UiComponentBase
//{
//    public string Placeholder { get; set; } = string.Empty;
//    public string Help { get; set; } = string.Empty;
//    public string? InitialValue { get; set; }
//}
public sealed class UiTextFieldDesign : UiComponentBase
{
    public UiTextFieldDesign()
    {
        ComponentType = typeof(UiTextField);
        Params["Title"] = "Text";
        Params["Placeholder"] = "Enter text";
        Params["Cols"] = 1;
        Params["CssClass"] = "";
        Params["Value"] = "";
        Params["Disabled"] = false;
    }

    public override IEnumerable<PropMeta> GetDesignProps() => new[]
    {
        new PropMeta("Title","Title","string",
            b => b.TryGetValue("Title", out var v) ? v : null,
            (b,v) => b["Title"] = v?.ToString()),

        new PropMeta("Placeholder","Placeholder","string",
            b => b.TryGetValue("Placeholder", out var v) ? v : null,
            (b,v) => b["Placeholder"] = v?.ToString()),

        new PropMeta("Cols","Columns","int",
            b => b.TryGetValue("Cols", out var v) ? v : 1,
            (b,v) => b["Cols"] = v is int i ? i : int.TryParse(v?.ToString(), out var ii) ? ii : 1),

        new PropMeta("CssClass","CSS Class","string",
            b => b.TryGetValue("CssClass", out var v) ? v : "",
            (b,v) => b["CssClass"] = v?.ToString()),

        new PropMeta("Value","Value","string",
            b => b.TryGetValue("Value", out var v) ? v : "",
            (b,v) => b["Value"] = v?.ToString()),

        new PropMeta("Disabled","Disabled","bool",
            b => b.TryGetValue("Disabled", out var v) && v is true,
            (b,v) => b["Disabled"] = v is true),
    };
}
