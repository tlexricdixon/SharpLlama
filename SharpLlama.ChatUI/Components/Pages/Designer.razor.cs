using SharpLlama.ChatUi.Service;
using SharpLlama.ChatUI.CiviTools.Models;

namespace SharpLlama.ChatUI.Components.Pages
{
    public partial class Designer
    {
        // Inject or pass in via ctor
        //public required ComponentRegistry Registry { get; init; }

        private readonly List<ComponentNode> Canvas = new();
        private ComponentNode? _selected;
        private IReadOnlyList<PropMeta> _designProps = Array.Empty<PropMeta>();

        private int? _dragIndex;
        private int? _dropIndex;

        // Add a new component from a registry descriptor
        public void AddComponent(ComponentRegistry.Descriptor d)
        {
            var comp = d.Factory();              // UiComponentBase (has Params + GetDesignProps)
            var node = new ComponentNode(comp);
            Canvas.Add(node);
            Select(node);
            StateHasChanged();
        }

        public void Select(ComponentNode node)
        {
            _selected = node;
            _designProps = _selected.Component.GetDesignProps().ToList();
            StateHasChanged();
        }

        // Property panel -> update selected node's parameter bag
        public void SetProp(PropMeta pm, object? raw)
        {
            if (_selected is null) return;
            var bag = _selected.Component.Params;

            object? val = raw;
            switch (pm.Type)
            {
                case "string":
                    val = raw?.ToString();
                    break;

                case "int":
                    if (raw is string s && int.TryParse(s, out var i)) val = i;
                    else if (raw is int i2) val = i2;
                    else val = null;
                    break;

                case "bool":
                    // checkbox e.Value can be bool or "on"/"true"
                    val = raw switch
                    {
                        bool b => b,
                        string sb => sb.Equals("true", StringComparison.OrdinalIgnoreCase) || sb.Equals("on", StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                    break;

                case "date":
                    if (raw is string sd && DateTime.TryParse(sd, out var d)) val = d.Date;
                    else if (raw is DateTime dd) val = dd.Date;
                    else val = null;
                    break;

                    // add "list" or other custom editors as needed
            }

            pm.Setter(bag, val);

            // Force re-render so <DynamicComponent Parameters="..."> re-applies props
            StateHasChanged();
        }

        public void MoveUp()
        {
            if (_selected is null) return;
            var i = Canvas.FindIndex(n => n == _selected);
            if (i > 0)
            {
                (Canvas[i - 1], Canvas[i]) = (Canvas[i], Canvas[i - 1]);
                StateHasChanged();
            }
        }

        public void MoveDown()
        {
            if (_selected is null) return;
            var i = Canvas.FindIndex(n => n == _selected);
            if (i >= 0 && i < Canvas.Count - 1)
            {
                (Canvas[i + 1], Canvas[i]) = (Canvas[i], Canvas[i + 1]);
                StateHasChanged();
            }
        }

        public void RemoveSelected()
        {
            if (_selected is null) return;
            Canvas.RemoveAll(n => n == _selected);
            _selected = null;
            _designProps = Array.Empty<PropMeta>();
            StateHasChanged();
        }

        public void OnDragStart(int index)
        {
            _dragIndex = index;
            _dropIndex = null;
        }

        public void OnDragOver(int index)
        {
            _dropIndex = index;
            StateHasChanged(); // show drop hint
        }

        public void OnDrop(int index)
        {
            if (_dragIndex is null) return;

            var from = _dragIndex.Value;
            var to = index;
            if (from == to) { _dragIndex = _dropIndex = null; return; }

            var item = Canvas[from];
            Canvas.RemoveAt(from);
            if (to > from) to--; // when dragging downward
            Canvas.Insert(to, item);

            _dragIndex = _dropIndex = null;
            StateHasChanged();
        }
        private string GetItemCss(int index)
        {
            var isSelected = _selected is not null &&
                             Canvas.IndexOf(_selected) == index;
            var isDropTarget = _dropIndex is not null && _dropIndex.Value == index;

            // Add your own classes; these are examples that pair with the CSS below.
            if (isDropTarget && isSelected) return "selected drag-target";
            if (isDropTarget) return "drag-target";
            if (isSelected) return "selected";
            return string.Empty;
        }
        private void OnDragLeave(int index)
        {
            if (_dropIndex == index) { _dropIndex = null; StateHasChanged(); }
        }

    }

}
