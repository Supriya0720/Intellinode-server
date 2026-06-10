namespace Intellinode.Infrastructure.Options;

public sealed class Windows8021xOptions
{
    public const string SectionName = "Windows8021x";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "Win802_1x";
}
