using Microsoft.AspNetCore.Components;
using SharpLlama.ChatUI.CiviTools.Models;

namespace SharpLlama.ChatUI.Components.UI;

public partial class UiDatePicker : UiInputBase<DateTime?>
{
    private async Task OnInput(ChangeEventArgs e)
    {
        DateTime? parsedValue = null;
        if (DateTime.TryParse(e?.Value?.ToString(), out var dt))
            parsedValue = dt;
        await SetCurrentValueAsync(parsedValue);
    }
}
//    [Parameter] public UiDatePicker Model { get; set; } = new();

//    // Canvas-editable props (map back into Model so designer/runtime stay in sync)
//    [Parameter] public string? Title { get; set; }
//    [Parameter] public int? Cols { get; set; }
//    [Parameter] public string? CssClass { get; set; }

//    // Two-way bindable value (date pickers want nullable DateTime)
//    [Parameter] public DateTime? Value { get; set; }
//    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }
//    [Parameter] public Expression<Func<DateTime?>>? ValueExpression { get; set; }

//    // Optional extras most UIs expect
//    [Parameter] public DateTime? Min { get; set; }
//    [Parameter] public DateTime? Max { get; set; }
//    [Parameter] public bool Disabled { get; set; }
//    [Parameter] public string? Placeholder { get; set; }
//    [CascadingParameter] protected EditContext? EditContext { get; set; }

//    protected string EffectiveTitle => string.IsNullOrWhiteSpace(Model.Title) ? "Date" : Model.Title!;
//    protected int EffectiveCols => (int)((Cols ?? Model.Cols) > 0 ? (Cols ?? Model.Cols) : 1);
//    protected string EffectiveCssClass => CssClass ?? Model.CssClass ?? "";

//    protected override void OnParametersSet()
//    {
//        if (Title is not null) Model.Title = Title;
//        if (Cols is int c) Model.Cols = c;
//        if (CssClass is not null) Model.CssClass = CssClass;

//        // Initial value default from model if caller didn't bind
//        // FIX: Removed reference to Model.InitialValue since UiDatePicker does not define it
//        // If you need an initial value, consider adding a property to UiDatePicker or set Value elsewhere
//    }

//    protected async Task SetCurrentValueAsync(DateTime? dt)
//    {
//        Value = dt;
//        if (EditContext != null && ValueExpression != null)
//            EditContext.NotifyFieldChanged(FieldIdentifier.Create(ValueExpression));
//        await ValueChanged.InvokeAsync(Value);
//        StateHasChanged();
//    }
//}



