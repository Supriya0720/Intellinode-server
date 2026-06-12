namespace Intellinode.Infrastructure.Options;

public sealed class WindowsRegionLocationOptions
{
    public const string SectionName = "WindowsRegionLocation";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "RLS";
}
