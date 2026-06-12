namespace Intellinode.Infrastructure.Options;

public sealed class WindowsScreenSaverOptions
{
    public const string SectionName = "WindowsScreenSaver";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "SCR";
}
