namespace Intellinode.Infrastructure.Options;

public sealed class WindowsEthernetSetupOptions
{
    public const string SectionName = "WindowsEthernetSetup";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "NT&Ethernet";
    public bool ValidateDuplicateIp { get; set; } = true;
}
