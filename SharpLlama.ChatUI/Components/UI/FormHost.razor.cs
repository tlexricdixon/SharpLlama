using Microsoft.AspNetCore.Components;
using SharpLlama.ChatUI.CiviTools.Models;

namespace SharpLlama.ChatUI.Components.UI;

public partial class FormHostBase : ComponentBase
{
    [Parameter]
    public IEnumerable<ComponentNode> Nodes { get; set; } = Array.Empty<ComponentNode>();

    // Renders a node (and its children) using DynamicComponent + parameter bag
    protected static RenderFragment RenderNode(ComponentNode node) => builder =>
    {
        // Render the node's component
        builder.OpenComponent(0, typeof(DynamicComponent));
        builder.AddAttribute(1, "Type", node.Component.ComponentType);
        builder.AddAttribute(2, "Parameters", node.Component.Params);
        builder.SetKey(node.Component.Id);
        builder.CloseComponent();

        // Render children (if any)
        if (node.Children is { Count: > 0 })
        {
            var seq = 1000;
            foreach (var child in node.Children)
            {
                builder.AddContent(seq++, RenderNode(child));
            }
        }
    };
}