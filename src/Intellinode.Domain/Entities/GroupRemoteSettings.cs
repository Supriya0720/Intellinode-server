using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public sealed class GroupRemoteSettings
{
    public Guid GroupId { get; set; }
    public DeviceGroup Group { get; set; } = null!;
    public string ServerHost { get; set; } = string.Empty;
    public int ServerPort { get; set; } = 443;
    public int PollIntervalSeconds { get; set; } = 300;
    public CommunicationType CommunicationType { get; set; } = CommunicationType.HTTPS;
    public bool AgentEnabled { get; set; } = true;
    public string? DesiredGroupName { get; set; }
    public string? AgentHostName { get; set; }
    public bool UseDhcpDiscovery { get; set; }
    public bool ApplyOnReboot { get; set; }
    public long SettingsVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
