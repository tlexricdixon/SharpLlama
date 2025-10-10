namespace SharpLlama.ChatUI.CiviTools.Models;

public sealed class UiGridDesign : UiComponentBase
{
    public UiGridDesign()
    {
        Params["Title"] = "Grid";
        Params["Cols"] = 3;
        Params["CssClass"] = "";
        Params["Rows"] = Array.Empty<object>(); // materialize outside in real use
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
        // Rows editor left out; usually bound programmatically not via panel
    };
}

