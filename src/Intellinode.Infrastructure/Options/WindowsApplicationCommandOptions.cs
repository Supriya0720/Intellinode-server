namespace Intellinode.Infrastructure.Options;

public sealed class WindowsApplicationCommandOptions
{
    public const string SectionName = "WindowsApplicationCommand";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "196";
}
