namespace SharpLlama.ChatUI.Components.UI;

using Microsoft.AspNetCore.Components;
using SharpLlama.ChatUI.CiviTools.Models;

public partial class UiTextField : UiInputBase<string>
{
    // No Model here — parameters come from the bag via UiInputBase<T>.

    // If you want any component-specific hooks, add them here.
    // The base already provides: Title, Placeholder, Help, Cols, CssClass, Disabled,
    // Value, ValueChanged, ValueExpression, EditContext, EffectiveCols, EffectiveTitle, EffectiveCssClass

    private async Task OnInput(ChangeEventArgs e)
        => await SetCurrentValueAsync(e?.Value?.ToString());
}
//    // Model is optional; direct parameters can override it.
//    [Parameter] public UiTextField? Model { get; set; }

//    // Canvas/property-panel friendly overrides (PascalCase matches parameter bag keys)
//    [Parameter] public string? Title { get; set; }
//    [Parameter] public string? Placeholder { get; set; }
//    [Parameter] public string? Help { get; set; }
//    [Parameter] public int? Cols { get; set; }
//    [Parameter] public string? CssClass { get; set; }
//    [Parameter] public bool Disabled { get; set; }

//    // Two-way binding
//    [Parameter] public string? Value { get; set; }
//    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
//    [Parameter] public Expression<Func<string?>>? ValueExpression { get; set; }

//    [CascadingParameter] protected EditContext? EditContext { get; set; }

//    // Effective values (never NRE)
//    protected string EffectiveTitle => string.IsNullOrWhiteSpace(Title ?? Model?.Title) ? "Text" : (Title ?? Model!.Title!);
//    protected string EffectivePlaceholder => (Placeholder ?? Model?.Placeholder) ?? string.Empty;
//    protected string EffectiveHelp => (Help ?? Model?.Help) ?? string.Empty;
//    protected int EffectiveCols => (Cols ?? Model?.Cols) is int c && c > 0 ? c : 1;
//    protected string EffectiveCssClass => (CssClass ?? Model?.CssClass) ?? string.Empty;

//    protected override void OnParametersSet()
//    {
//        // Seed Value from model only if caller didn't bind a value yet.
//        if (Value is null && !string.IsNullOrEmpty(Model?.InitialValue))
//            Value = Model!.InitialValue;

//        // If you want the designer/runtime to stay in sync, push overrides back into Model.
//        if (Model is not null)
//        {
//            if (Title is not null) Model.Title = Title;
//            if (Placeholder is not null) Model.Placeholder = Placeholder;
//            if (Help is not null) Model.Help = Help;
//            if (Cols is int c) Model.Cols = c;
//            if (CssClass is not null) Model.CssClass = CssClass;
//        }
//    }

//    protected async Task SetCurrentValueAsync(string? v)
//    {
//        Value = v;
//        if (EditContext != null && ValueExpression != null)
//            EditContext.NotifyFieldChanged(FieldIdentifier.Create(ValueExpression));
//        await ValueChanged.InvokeAsync(Value);
//        StateHasChanged();
//    }

//    // If you're still on the old static design-props system, keep these.
//    // If you migrated to instance-based GetDesignProps(), you can drop them.
//    public static IReadOnlyList<PropMeta> DesignProps { get; } = UiTextFieldExtensions.DesignPropsStatic();
//    public static IEnumerable<PropMeta> PropMetas => DesignProps;
//}
//public sealed class UiTextFieldDesign : UiComponentBase
//{
//    public UiTextFieldDesign()
//    {
//        ComponentType = typeof(UiTextField); // your .razor
//        Params["Title"] = "Text";
//        Params["Placeholder"] = "Enter text";
//        Params["Cols"] = 1;
//        Params["CssClass"] = "";
//        Params["Value"] = "";
//        Params["Disabled"] = false;
//    }

//    public override IEnumerable<PropMeta> GetDesignProps() => new[]
//    {
//        new PropMeta("Title","Title","string",
//            bag => bag.TryGetValue("Title", out var v) ? v : null,
//            (bag,v) => bag["Title"] = v?.ToString()
//        ),
//        new PropMeta("Placeholder","Placeholder","string",
//            bag => bag.TryGetValue("Placeholder", out var v) ? v : null,
//            (bag,v) => bag["Placeholder"] = v?.ToString()
//        ),
//        new PropMeta("Cols","Columns","int",
//            bag => bag.TryGetValue("Cols", out var v) ? v : 1,
//            (bag,v) => bag["Cols"] = v is int i ? i : int.TryParse(v?.ToString(), out var ii) ? ii : 1
//        ),
//        new PropMeta("CssClass","CSS Class","string",
//            bag => bag.TryGetValue("CssClass", out var v) ? v : "",
//            (bag,v) => bag["CssClass"] = v?.ToString()
//        ),
//        new PropMeta("Value","Value","string",
//            bag => bag.TryGetValue("Value", out var v) ? v : "",
//            (bag,v) => bag["Value"] = v?.ToString()
//        ),
//        new PropMeta("Disabled","Disabled","bool",
//            bag => bag.TryGetValue("Disabled", out var v) && v is true,
//            (bag,v) => bag["Disabled"] = v is true
//        ),
//    };
//}