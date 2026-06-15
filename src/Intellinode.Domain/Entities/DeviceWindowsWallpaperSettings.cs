namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows wallpaper settings per device (FusionX User Settings → Wallpaper).
/// <see cref="RepositoryJson"/> holds FTP/repository metadata for PR3 upload path (ADR-0006 Option B).
/// </summary>
public sealed class DeviceWindowsWallpaperSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string SourceType { get; set; } = "Browse";
    /// <summary>Browse path on device (<c>strPictureName</c> when <see cref="Upload"/> is false).</summary>
    public string PicturePath { get; set; } = string.Empty;
    /// <summary>Upload/repository file name (<c>strPictureName</c> when <see cref="Upload"/> is true).</summary>
    public string PictureName { get; set; } = string.Empty;
    public string PicturePosition { get; set; } = string.Empty;
    public bool PreventUserChanges { get; set; }
    public bool Upload { get; set; }
    public int AgentAction { get; set; }
    /// <summary>FusionX repository/FTP fields as JSON (PR3 hydration). Null for browse-only.</summary>
    public string? RepositoryJson { get; set; }
    public long SettingsVersion { get; set; } = 1;
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
