namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows taskbar settings per device (FusionX User Settings → Taskbar Properties).
/// </summary>
public sealed class DeviceWindowsTaskbarSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public bool LockTaskbar { get; set; } = true;
    public bool AutoHideTaskbar { get; set; }
    public bool KeepTaskbarOnTop { get; set; } = true;
    public bool GroupSimilarButtons { get; set; } = true;
    public bool ShowQuickLaunch { get; set; }
    public int AgentAction { get; set; }
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
