namespace Intellinode.Infrastructure.Options;

public sealed class WindowsDateTimeOptions
{
    public const string SectionName = "WindowsDateTime";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string ManualDateTimeSignalSuffix { get; set; } = "DT";
    public string TimeZoneSignalSuffix { get; set; } = "TZ";
    public string TimeServerSignalSuffix { get; set; } = "TS";
}
