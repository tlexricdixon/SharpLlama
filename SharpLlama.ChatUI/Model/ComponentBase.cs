namespace SharpLlama.ChatUI.CiviTools.Models
{
    public class ComponentBase<T> : UiComponentBase
    {
        public string Placeholder { get; set; } = string.Empty;
        public string Help { get; set; } = string.Empty;
    }
}
