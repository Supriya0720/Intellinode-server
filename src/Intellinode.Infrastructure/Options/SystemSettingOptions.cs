namespace Intellinode.Infrastructure.Options;

public sealed class SystemSettingOptions
{
    public const string SectionName = "SystemSetting";

    public bool Enabled { get; set; }
    public bool ReadOnly { get; set; }
    public bool DualWrite { get; set; } = true;
    public bool LegacySummaryEnabled { get; set; } = true;
}
