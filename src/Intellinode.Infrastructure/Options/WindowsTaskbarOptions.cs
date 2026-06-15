namespace Intellinode.Infrastructure.Options;

public sealed class WindowsTaskbarOptions
{
    public const string SectionName = "WindowsTaskbar";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public bool AgentLiveReadEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "TPR";
}
