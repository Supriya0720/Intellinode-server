namespace Intellinode.Infrastructure.Options;

public sealed class WindowsComputerNameOptions
{
    public const string SectionName = "WindowsComputerName";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "CN";
}
