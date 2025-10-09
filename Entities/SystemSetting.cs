namespace SharpLlama.Entities;

public partial class SystemSetting
{
    public short SettingId { get; set; }

    public string? Notes { get; set; }

    public string? SettingName { get; set; }

    public string? SettingValue { get; set; }
}
