using Microsoft.AspNetCore.Components;
using SharpLlama.ChatUI.CiviTools.Models;

namespace SharpLlama.ChatUI.Components.UI;

public partial class UiSelect : UiInputBase<string>
{
    private async Task OnInput(ChangeEventArgs e)
        => await SetCurrentValueAsync(e?.Value?.ToString());
}
//    [Parameter] public UiSelect Model { get; set; } = new();

//    // Canvas/property-panel editable
//    [Parameter] public string? Title { get; set; }
//    [Parameter] public int? Cols { get; set; }
//    [Parameter] public string? CssClass { get; set; }

//    // Data + selection
//    [Parameter] public IEnumerable<string>? Items { get; set; }    // simple string list for now
//    [Parameter] public string? Value { get; set; }
//    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
//    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

//    // UX flags
//    [Parameter] public bool Disabled { get; set; }
//    [Parameter] public bool AllowEmptyOption { get; set; } = true;
//    [Parameter] public string EmptyLabel { get; set; } = "-- select --";

//    [CascadingParameter] protected EditContext? EditContext { get; set; }

//    protected string EffectiveTitle => string.IsNullOrWhiteSpace(Model.Title) ? "Select" : Model.Title!;
//    protected int EffectiveCols => (int)((Cols ?? Model.Cols) > 0 ? (Cols ?? Model.Cols) : 1);
//    protected string EffectiveCssClass => CssClass ?? Model.CssClass ?? string.Empty;
//    protected IEnumerable<string> EffectiveItems => Items ?? Model.Items ?? Array.Empty<string>();

//    protected override void OnParametersSet()
//    {
//        if (Title is not null) Model.Title = Title;
//        if (Cols is int c) Model.Cols = c;
//        if (CssClass is not null) Model.CssClass = CssClass;
//        if (Items is not null) Model.Items = Items.ToList();

//        // Seed initial value from model if caller didn't bind
//        //if (Value is null && !string.IsNullOrEmpty(Model.InitialValue))
//        //    Value = Model.InitialValue;
//    }

//    protected async Task SetCurrentValueAsync(string? v)
//    {
//        Value = v;
//        if (EditContext != null && ValueExpression != null)
//            EditContext.NotifyFieldChanged(FieldIdentifier.Create(ValueExpression));
//        await ValueChanged.InvokeAsync(Value);
//        StateHasChanged();
//    }

//    public static IReadOnlyList<PropMeta> DesignProps { get; } = UiSelectExtensions.DesignPropsStatic();
//    public static IEnumerable<PropMeta> PropMetas => DesignProps;
//}



