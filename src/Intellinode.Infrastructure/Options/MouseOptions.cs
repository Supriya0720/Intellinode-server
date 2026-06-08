namespace Intellinode.Infrastructure.Options;

public sealed class MouseOptions
{
    public const string SectionName = "Mouse";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "SCR";
}
