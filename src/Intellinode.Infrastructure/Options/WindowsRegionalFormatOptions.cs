namespace Intellinode.Infrastructure.Options;

public sealed class WindowsRegionalFormatOptions
{
    public const string SectionName = "WindowsRegionalFormat";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "RS";
}
