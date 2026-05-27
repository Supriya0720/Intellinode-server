using Intellinode.Domain.Enums;

namespace Intellinode.Application.Contracts.Agents;

public sealed class DeviceRemoteSettingsDto
{
    public string MacAddress { get; set; } = string.Empty;
    public string ServerHost { get; set; } = string.Empty;
    public int ServerPort { get; set; }
    public int PollIntervalSeconds { get; set; }
    public CommunicationType CommunicationType { get; set; }
    public bool AgentEnabled { get; set; }
    public string? DesiredGroupName { get; set; }
    public string? AgentHostName { get; set; }
    public bool UseDhcpDiscovery { get; set; }
    public bool ApplyOnReboot { get; set; }
    public bool InheritFromGroup { get; set; } = true;
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// Admin PUT body for per-device desired remote settings.
/// Empty <see cref="ServerHost"/> means use the tenant default server URL.
/// </summary>
public sealed class UpsertDeviceRemoteSettingsRequest
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

public sealed class AgentConfigResponse
{
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; }
    public CommunicationType CommunicationType { get; set; }
    public bool AgentEnabled { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public AgentAdvancedConfigDto Advanced { get; set; } = new();
}

public sealed class EffectiveAgentSettings
{
    public string ServerBaseUrl { get; init; } = string.Empty;
    public string ApiBaseUrl { get; init; } = string.Empty;
    public int PollIntervalSeconds { get; init; }
    public CommunicationType CommunicationType { get; init; }
    public bool AgentEnabled { get; init; } = true;
    public long SettingsVersion { get; init; }
    public bool PendingApply { get; init; }
    public string Source { get; init; } = string.Empty;
}
