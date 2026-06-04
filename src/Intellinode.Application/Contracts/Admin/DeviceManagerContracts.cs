using System.Text.Json;
using System.Text.Json.Serialization;
using Intellinode.Domain.Enums;

namespace Intellinode.Application.Contracts.Admin;

public sealed class DeviceTreeQuery
{
    public string? Search { get; set; }
    public string Status { get; set; } = "All";
    public Guid? RootGroupId { get; set; }
    public bool IncludeUnassigned { get; set; } = true;
}

public enum DeviceManagerNodeType
{
    Group,
    Device,
    Unassigned
}

public sealed class DeviceTreeNodeDto
{
    public Guid Id { get; set; }
    public string NodeType { get; set; } = string.Empty;
    [JsonPropertyName("nodename")]
    public string NodeName { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int Depth { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
    public int? DeviceCount { get; set; }
    public int? OnlineCount { get; set; }
    public int? OfflineCount { get; set; }
    public int? MaintenanceCount { get; set; }
    public string? MacAddress { get; set; }
    public string? IpAddress { get; set; }
    public string? Status { get; set; }
    public int? BatteryPercent { get; set; }
    public string? AgentType { get; set; }
    public string? OsPlatform { get; set; }
    public bool? IsOnline { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public EnrollmentState? EnrollmentState { get; set; }
    public IReadOnlyList<DeviceTreeNodeDto>? subRows { get; set; }
}

public sealed class DeviceTreeResponse
{
    public IReadOnlyList<DeviceTreeNodeDto> Items { get; set; } = [];
    public int TotalDeviceCount { get; set; }
    public int FilteredDeviceCount { get; set; }
}

public sealed class DeviceManagerGroupChildDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int DeviceCount { get; set; }
}

public sealed class DeviceManagerGroupRecentDeviceDto
{
    public Guid Id { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastHeartbeatUtc { get; set; }
}

public sealed class DeviceManagerGroupInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedUtc { get; set; }
    public IReadOnlyList<string> Breadcrumb { get; set; } = [];
    public IReadOnlyList<DeviceManagerGroupChildDto> ChildGroups { get; set; } = [];
    public int DirectChildGroupCount { get; set; }
    public int TotalDevices { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
    public int MaintenanceCount { get; set; }
    public int StaleCount { get; set; }
    public bool HasRemoteSettings { get; set; }
    public bool HasAdvancedSettings { get; set; }
    public long? RemoteSettingsVersion { get; set; }
    public long? AdvancedSettingsVersion { get; set; }
    public IReadOnlyList<DeviceManagerGroupRecentDeviceDto> RecentDevices { get; set; } = [];
}

public sealed class DeviceManagerDeviceGroupRefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Breadcrumb { get; set; } = [];
}

public sealed class DeviceManagerDeviceInventoryDto
{
    public JsonElement? Hardware { get; set; }
    public JsonElement? Network { get; set; }
    public JsonElement? OsInfo { get; set; }
    public JsonElement? Security { get; set; }
    public DateTime? CollectedUtc { get; set; }
    public int? Version { get; set; }
}

public sealed class DeviceManagerDeviceInfoDto
{
    public Guid Id { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? BatteryPercent { get; set; }
    public string AgentType { get; set; } = string.Empty;
    public string OsPlatform { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public string ClientStatus { get; set; } = string.Empty;
    public EnrollmentState EnrollmentState { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string CommunicationIpAddress { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Workgroup { get; set; } = string.Empty;
    public string LoginUserName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string CommunicationType { get; set; } = string.Empty;
    public int PollInterval { get; set; }
    public string AgentUpTime { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public bool IsRegistered { get; set; }
    public bool IsLicensed { get; set; }
    public bool IsServiceMode { get; set; }
    public bool IsDhcp { get; set; }
    public bool IsDomainJoined { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DeviceManagerDeviceGroupRefDto? Group { get; set; }
    public DeviceManagerDeviceInventoryDto? Inventory { get; set; }
    public bool? InheritFromGroup { get; set; }
    public bool RemoteSettingsPendingApply { get; set; }
    public bool AdvancedSettingsPendingApply { get; set; }
}
