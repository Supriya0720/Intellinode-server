using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows date, time zone, and NTP settings per device (FusionX System Settings → Time and Language → Date &amp; Time).
/// </summary>
public sealed class DeviceWindowsDateTimeSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public WindowsDateTimeApplyMode ApplyMode { get; set; }
    public DateOnly? CurrentDateLocal { get; set; }
    public TimeOnly? CurrentTimeLocal { get; set; }
    public string? TimeZoneDisplay { get; set; }
    public string? WindowsTzKey { get; set; }
    public string? TimeServer { get; set; }
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
