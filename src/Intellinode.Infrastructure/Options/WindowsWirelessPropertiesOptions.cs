namespace Intellinode.Infrastructure.Options;

public sealed class WindowsWirelessPropertiesOptions
{
    public const string SectionName = "WindowsWirelessProperties";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "WNS";
}
