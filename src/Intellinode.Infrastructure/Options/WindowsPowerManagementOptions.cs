namespace Intellinode.Infrastructure.Options;

public sealed class WindowsPowerManagementOptions
{
    public const string SectionName = "WindowsPowerManagement";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "PMO";
}
