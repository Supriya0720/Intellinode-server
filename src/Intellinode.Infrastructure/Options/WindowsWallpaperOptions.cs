namespace Intellinode.Infrastructure.Options;

public sealed class WindowsWallpaperOptions
{
    public const string SectionName = "WindowsWallpaper";

    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool LegacySummaryEnabled { get; set; } = true;
    public string DefaultSignalSuffix { get; set; } = "WPS";
}
