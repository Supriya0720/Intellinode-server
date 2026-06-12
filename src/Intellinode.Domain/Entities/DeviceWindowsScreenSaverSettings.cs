namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows screen saver settings per device (FusionX User Settings → Screen Saver).
/// <see cref="RepositoryJson"/> holds FTP/repository metadata for PR3 upload path (ADR-0005 Option B).
/// </summary>
public sealed class DeviceWindowsScreenSaverSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string ScreenSaverName { get; set; } = string.Empty;
    public int TimeoutMinutes { get; set; }
    public bool PasswordProtected { get; set; }
    public bool PreventUserChanges { get; set; }
    public string SourceType { get; set; } = "Browse";
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
