using Intellinode.Domain.Enums;

namespace Intellinode.Application.Contracts.Admin;

public sealed class DeviceManagerRootsQuery
{
    public string Status { get; set; } = "All";
    public string? Search { get; set; }
    public bool IncludeUnassigned { get; set; } = true;
}

public sealed class DeviceManagerGroupChildrenQuery
{
    public string Status { get; set; } = "All";
    public string? Search { get; set; }
}

public sealed class DeviceManagerGroupDevicesQuery
{
    public string Status { get; set; } = "All";
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string SortBy { get; set; } = "HostName";
    public string SortDir { get; set; } = "asc";
}

public sealed class DeviceManagerGroupSummaryDto
{
    public Guid Id { get; set; }
    public DeviceManagerNodeType NodeType { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int Depth { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }
    public int DeviceCount { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
    public int MaintenanceCount { get; set; }
}

public sealed class DeviceManagerDeviceRowDto
{
    public Guid Id { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? BatteryPercent { get; set; }
    public string AgentType { get; set; } = string.Empty;
    public string OsPlatform { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public EnrollmentState EnrollmentState { get; set; }
    public Guid? GroupId { get; set; }
}

public sealed class DeviceManagerRootsResponse
{
    public IReadOnlyList<DeviceManagerGroupSummaryDto> Items { get; set; } = [];
    public int TotalDeviceCount { get; set; }
    public int FilteredDeviceCount { get; set; }
}

public sealed class DeviceManagerChildGroupsResponse
{
    public Guid ParentGroupId { get; set; }
    public string ParentGroupName { get; set; } = string.Empty;
    public int ParentDepth { get; set; }
    public IReadOnlyList<DeviceManagerGroupSummaryDto> Items { get; set; } = [];
}

public sealed class PagedDeviceManagerDevicesResponse
{
    public IReadOnlyList<DeviceManagerDeviceRowDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
}
