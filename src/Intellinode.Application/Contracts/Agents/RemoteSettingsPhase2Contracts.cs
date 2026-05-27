using Intellinode.Domain.Enums;

namespace Intellinode.Application.Contracts.Agents;

public  class AgentAdvancedConfigDto
{
    public int DebugLevel { get; set; }
    public int HeartbeatIntervalSeconds { get; set; }
    public int ApplicationIntervalSeconds { get; set; }
    public bool UsbLogsEnabled { get; set; }
    public bool ApplicationLogsEnabled { get; set; }
    public bool BootLogsEnabled { get; set; }
    public bool ScreensaverLogsEnabled { get; set; }
    public bool YumMonitorEnabled { get; set; }
    public bool SignalrMonitoringEnabled { get; set; }
    public CommunicationType ConnectionType { get; set; }
    public int DhcpPollIntervalSeconds { get; set; }
    public bool AlwaysApply { get; set; }
    public bool ApplyOnNextReboot { get; set; }
    public long AdvancedSettingsVersion { get; set; }
    public bool AdvancedPendingApply { get; set; }
}

public sealed class DeviceAgentAdvancedSettingsDto : AgentAdvancedConfigDto
{
    public string MacAddress { get; set; } = string.Empty;
    public bool InheritFromGroup { get; set; } = true;
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? ExtraJson { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class UpsertDeviceAgentAdvancedSettingsRequest
{
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
    public string? ExtraJson { get; set; }
}

public sealed class GroupRemoteSettingsDto
{
    public Guid GroupId { get; set; }
    public string ServerHost { get; set; } = string.Empty;
    public int ServerPort { get; set; }
    public int PollIntervalSeconds { get; set; }
    public CommunicationType CommunicationType { get; set; }
    public bool AgentEnabled { get; set; }
    public string? DesiredGroupName { get; set; }
    public string? AgentHostName { get; set; }
    public bool UseDhcpDiscovery { get; set; }
    public bool ApplyOnReboot { get; set; }
    public long SettingsVersion { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class UpsertGroupRemoteSettingsRequest
{
    public string ServerHost { get; set; } = string.Empty;
    public int ServerPort { get; set; } = 443;
    public int PollIntervalSeconds { get; set; } = 300;
    public CommunicationType CommunicationType { get; set; } = CommunicationType.HTTPS;
    public bool AgentEnabled { get; set; } = true;
    public string? DesiredGroupName { get; set; }
    public string? AgentHostName { get; set; }
    public bool UseDhcpDiscovery { get; set; }
    public bool ApplyOnReboot { get; set; }
}

public sealed class GroupAgentAdvancedSettingsDto
{
    public Guid GroupId { get; set; }
    public int DebugLevel { get; set; }
    public int HeartbeatIntervalSeconds { get; set; }
    public int ApplicationIntervalSeconds { get; set; }
    public bool UsbLogsEnabled { get; set; }
    public bool ApplicationLogsEnabled { get; set; }
    public bool BootLogsEnabled { get; set; }
    public bool ScreensaverLogsEnabled { get; set; }
    public bool YumMonitorEnabled { get; set; }
    public bool SignalrMonitoringEnabled { get; set; }
    public CommunicationType ConnectionType { get; set; }
    public int DhcpPollIntervalSeconds { get; set; }
    public bool AlwaysApply { get; set; }
    public bool ApplyOnNextReboot { get; set; }
    public long SettingsVersion { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class UpsertGroupAgentAdvancedSettingsRequest
{
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
}

public sealed class EffectiveDeviceSettingsDto
{
    public string MacAddress { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public bool GeneralInheritFromGroup { get; set; }
    public bool AdvancedInheritFromGroup { get; set; }
    public string GeneralSource { get; set; } = string.Empty;
    public string AdvancedSource { get; set; } = string.Empty;
    public EffectiveAgentSettings General { get; set; } = new();
    public AgentAdvancedConfigDto Advanced { get; set; } = new();
}

public sealed class PatchDeviceSettingsInheritanceRequest
{
    public bool InheritFromGroup { get; set; } = true;
}

public sealed class AgentConfigAckRequest
{
    public long SettingsVersion { get; set; }
    public long AdvancedSettingsVersion { get; set; }
    public bool GeneralApplied { get; set; }
    public bool AdvancedApplied { get; set; }
}

public sealed class AgentConfigAckResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public AgentConfigResponse? Config { get; set; }
}

public sealed class PropagateGroupSettingsResponse
{
    public Guid GroupId { get; set; }
    public int DevicesMarkedPending { get; set; }
}
