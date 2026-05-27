using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public sealed class DeviceRemoteSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string ServerHost { get; set; } = string.Empty;
    public int ServerPort { get; set; } = 443;
    public int PollIntervalSeconds { get; set; } = 300;
    public CommunicationType CommunicationType { get; set; } = CommunicationType.HTTPS;
    public bool AgentEnabled { get; set; } = true;
    public string? DesiredGroupName { get; set; }
    public string? AgentHostName { get; set; }
    public bool UseDhcpDiscovery { get; set; }
    public bool ApplyOnReboot { get; set; }
    public bool InheritFromGroup { get; set; } = true;
    public long SettingsVersion { get; set; } = 1;
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
