namespace Intellinode.Infrastructure.Options;

public sealed class DisplayOptions
{
    public const string SectionName = "Display";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "SCR";
}
