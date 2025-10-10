namespace SharpLlama.ChatUI.Components.UI;

public partial class UiGrid //: UiInputBase<string>
{
    //private async Task OnInput(ChangeEventArgs e)
    //    => await SetCurrentValueAsync(e?.Value?.ToString());
}
//    // Underlying model still works (helpful defaults, design metadata, etc.)
//    [Parameter] public UiGrid Model { get; set; } = new();

//    // Props the *canvas/property editor* can tweak live
//    [Parameter] public string? Title { get; set; }
//    [Parameter] public int? Cols { get; set; }              // <- user can set after drop
//    [Parameter] public string? CssClass { get; set; }
//    [Parameter] public IEnumerable<object>? Rows { get; set; }

//    // Optional: let callers override how a cell renders
//    [Parameter] public RenderFragment<(object Row, PropertyInfo Col)>? CellTemplate { get; set; }

//    protected string EffectiveTitle => string.IsNullOrWhiteSpace(Model.Title) ? "Text" : Model.Title!;
//    protected int EffectiveCols => (int)((Cols ?? Model.Cols) > 0 ? (Cols ?? Model.Cols) : 1);
//    protected string EffectiveCssClass => CssClass ?? Model.CssClass ?? string.Empty;
//    protected IEnumerable<object> EffectiveRows => Rows ?? Model.Rows ?? Array.Empty<object>();

//    protected IReadOnlyList<PropertyInfo> Columns { get; private set; } = Array.Empty<PropertyInfo>();
//    protected string GridStyle => $"display:grid;grid-template-columns:repeat({EffectiveCols},minmax(0,1fr));gap:.75rem;";

//    protected override void OnParametersSet()
//    {
//        // Push param overrides back into the model so runtime + designer stay in sync
//        if (Title is not null) Model.Title = Title;
//        if (Cols is int c) Model.Cols = c;
//        if (CssClass is not null) Model.CssClass = CssClass;
//        if (Rows is not null) Model.Rows = Rows;

//        var first = EffectiveRows.FirstOrDefault();
//        Columns = first?.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
//                  ?? Array.Empty<PropertyInfo>();
//    }

//    // Design-time metadata you already use
//    public static IReadOnlyList<PropMeta> DesignProps { get; } = UiGridExtensions.DesignPropsStatic();
//    public static IEnumerable<PropMeta> PropMetas => DesignProps;
//}



