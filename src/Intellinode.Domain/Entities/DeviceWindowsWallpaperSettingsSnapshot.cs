namespace Intellinode.Domain.Entities;

/// <summary>
/// Immutable wallpaper state captured at queue time for a specific <see cref="SettingsVersion"/>.
/// Enables agent hydration for repository/upload tasks after the live row advances.
/// </summary>
public sealed class DeviceWindowsWallpaperSettingsSnapshot
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public long SettingsVersion { get; set; }
    public string SourceType { get; set; } = "Browse";
    public string PicturePath { get; set; } = string.Empty;
    public string PictureName { get; set; } = string.Empty;
    public string PicturePosition { get; set; } = string.Empty;
    public bool PreventUserChanges { get; set; }
    public bool Upload { get; set; }
    public int AgentAction { get; set; }
    public string? RepositoryJson { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
