namespace SharpLlama.ChatUI.CiviTools.Models;

public sealed class UiSelectDesign : UiComponentBase
{
    public UiSelectDesign()
    {
        Params["Title"] = "Select";
        Params["Cols"] = 1;
        Params["CssClass"] = "";
        Params["Items"] = new[] { "One", "Two", "Three" };
        Params["Value"] = "";
        Params["Disabled"] = false;
        Params["AllowEmptyOption"] = true;
        Params["EmptyLabel"] = "-- select --";
    }

    public override IEnumerable<PropMeta> GetDesignProps() => new[]
    {
        new PropMeta("Title","Title","string",
            b => b["Title"], (b,v) => b["Title"] = v?.ToString()
        ),
        new PropMeta("Cols","Columns","int",
            b => b["Cols"], (b,v) => b["Cols"] = v is int i ? i : int.TryParse(v?.ToString(), out var ii) ? ii : 1
        ),
        new PropMeta("CssClass","CSS Class","string",
            b => b["CssClass"], (b,v) => b["CssClass"] = v?.ToString()
        ),
        new PropMeta("Items","Items (CSV)","string",
            b => string.Join(",", (b["Items"] as IEnumerable<string>) ?? Array.Empty<string>()),
            (b,v) => b["Items"] = v?.ToString()?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>()
        ),
        new PropMeta("Value","Value","string",
            b => b["Value"], (b,v) => b["Value"] = v?.ToString()
        ),
        new PropMeta("AllowEmptyOption","Allow Empty","bool",
            b => b.TryGetValue("AllowEmptyOption", out var v) && v is true,
            (b,v) => b["AllowEmptyOption"] = v is true
        ),
        new PropMeta("EmptyLabel","Empty Label","string",
            b => b["EmptyLabel"], (b,v) => b["EmptyLabel"] = v?.ToString()
        ),
        new PropMeta("Disabled","Disabled","bool",
            b => b.TryGetValue("Disabled", out var v) && v is true,
            (b,v) => b["Disabled"] = v is true
        ),
    };
}