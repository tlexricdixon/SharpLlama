namespace SharpLlama.ChatUI.CiviTools.Models;

public sealed class UiDatePickerDesign : UiComponentBase
{
    public UiDatePickerDesign()
    {
        Params["Title"] = "Date";
        Params["Cols"] = 1;
        Params["CssClass"] = "";
        Params["Value"] = null;
        Params["Min"] = null;
        Params["Max"] = null;
        Params["Disabled"] = false;
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
        new PropMeta("Value","Value","date",
            b => b.TryGetValue("Value", out var v) ? v : null,
            (b,v) => b["Value"] = v is DateTime dt ? dt.Date : (v is string s && DateTime.TryParse(s, out var d) ? d.Date : null)
        ),
        new PropMeta("Min","Min","date",
            b => b.TryGetValue("Min", out var v) ? v : null,
            (b,v) => b["Min"] = v is DateTime dt ? dt.Date : (v is string s && DateTime.TryParse(s, out var d) ? d.Date : null)
        ),
        new PropMeta("Max","Max","date",
            b => b.TryGetValue("Max", out var v) ? v : null,
            (b,v) => b["Max"] = v is DateTime dt ? dt.Date : (v is string s && DateTime.TryParse(s, out var d) ? d.Date : null)
        ),
        new PropMeta("Disabled","Disabled","bool",
            b => b.TryGetValue("Disabled", out var v) && v is true,
            (b,v) => b["Disabled"] = v is true
        ),
    };
}

