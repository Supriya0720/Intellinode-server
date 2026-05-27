using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public sealed class DeviceAgentAdvancedSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public int DebugLevel { get; set; }
    public int HeartbeatIntervalSeconds { get; set; } = 300;
    public int ApplicationIntervalSeconds { get; set; } = 60;
    public bool UsbLogsEnabled { get; set; }
    public bool ApplicationLogsEnabled { get; set; }
    public bool BootLogsEnabled { get; set; }
    public bool ScreensaverLogsEnabled { get; set; }
    public bool YumMonitorEnabled { get; set; }
    public bool SignalrMonitoringEnabled { get; set; }
    public CommunicationType ConnectionType { get; set; } = CommunicationType.HTTPS;
    public int DhcpPollIntervalSeconds { get; set; } = 300;
    public bool AlwaysApply { get; set; }
    public bool ApplyOnNextReboot { get; set; }
    public bool InheritFromGroup { get; set; } = true;
    public long SettingsVersion { get; set; } = 1;
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? ExtraJson { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
