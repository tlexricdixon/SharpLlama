namespace SharpLlama.ChatUI.CiviTools.Models;
using System;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

public abstract class UiInputBase<T> : ComponentBase
{
    [CascadingParameter] protected EditContext? EditContext { get; set; }

    [Parameter] public T? Value { get; set; }
    [Parameter] public EventCallback<T?> ValueChanged { get; set; }
    [Parameter] public Expression<Func<T?>>? ValueExpression { get; set; }

    [Parameter] public string? Title { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? Help { get; set; }
    [Parameter] public int? Cols { get; set; }
    [Parameter] public string? CssClass { get; set; }
    [Parameter] public bool Disabled { get; set; }

    protected int EffectiveCols => Cols is int c && c > 0 ? c : 1;
    protected string EffectiveTitle => Title ?? string.Empty;
    protected string EffectiveCssClass => CssClass ?? string.Empty;

    protected async Task SetCurrentValueAsync(T? v)
    {
        Value = v;
        if (EditContext != null && ValueExpression != null)
            EditContext.NotifyFieldChanged(FieldIdentifier.Create(ValueExpression));
        await ValueChanged.InvokeAsync(Value);
        StateHasChanged();
    }
}

