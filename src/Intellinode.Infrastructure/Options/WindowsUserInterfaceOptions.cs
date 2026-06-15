namespace Intellinode.Infrastructure.Options;

public sealed class WindowsUserInterfaceOptions
{
    public const string SectionName = "WindowsUserInterface";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    /// <summary>FusionX async signal suffix for Autologon tasks (verify against live agent if empty).</summary>
    public string DefaultSignalSuffix { get; set; } = string.Empty;
}
