namespace Intellinode.Infrastructure.Options;

public sealed class KeyboardOptions
{
    public const string SectionName = "Keyboard";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "SCR";
}
