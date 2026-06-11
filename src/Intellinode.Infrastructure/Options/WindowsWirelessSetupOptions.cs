namespace Intellinode.Infrastructure.Options;

public sealed class WindowsWirelessSetupOptions
{
    public const string SectionName = "WindowsWirelessSetup";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "W";
    public bool ValidateDuplicateIp { get; set; } = true;
    public bool RequireWirelessAdapter { get; set; }
}
