using SharpLlama.ChatUI.CiviTools.Models;
using SharpLlama.ChatUI.Components.UI;

namespace SharpLlama.ChatUi.Service;

public class ComponentRegistry
{
    public record Descriptor(string TypeKey, string DisplayName, Func<UiComponentBase> Factory);

    private readonly List<Descriptor> _descriptors = new();

    public ComponentRegistry()
    {
        Register(new Descriptor(
            "text", "Text Field",
            () => new UiTextFieldDesign
            {
                ComponentType = typeof(SharpLlama.ChatUI.Components.UI.UiTextField),
            }
        ));
        Register(new Descriptor(
            "select", "Select",
            () => new UiSelectDesign
            {
                ComponentType = typeof(UiSelect),
            }
        ));
        Register(new Descriptor(
            "date", "Date Picker",
            () => new UiDatePickerDesign
            {
                ComponentType = typeof(UiDatePicker),
            }
        ));
        Register(new Descriptor(
            "grid", "Grid",
            () => new UiGridDesign
            {
                ComponentType = typeof(UiGrid),
            }
        ));
    }

    public void Register(Descriptor d)
    {
        // Optional: fail fast if a bad type slips in
        var probe = d.Factory();
        if (probe.ComponentType is null || !typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(probe.ComponentType))
            throw new InvalidOperationException(
                $"Descriptor '{d.DisplayName}' has invalid ComponentType '{probe.ComponentType?.FullName ?? "null"}'.");

        _descriptors.Add(new Descriptor(d.TypeKey, d.DisplayName, () => probe));
    }

    public IEnumerable<Descriptor> All() => _descriptors;
}

