using System.Text.Json;
using Intellinode.Domain.Enums;

namespace Intellinode.Application.Contracts.Admin;

public sealed class DiscoverLookupQuery
{
    public string Status { get; set; } = "All";
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "DiscoveredUtc";
    public string SortDir { get; set; } = "desc";
}

public sealed class DiscoverLookupListItemDto
{
    public Guid Id { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public DiscoverLookupStatus Status { get; set; }
    public DateTime DiscoveredUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public Guid? DeviceId { get; set; }
    public EnrollmentState? DeviceEnrollmentState { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public string? GroupName { get; set; }
}

public sealed class PagedDiscoverLookupResponse
{
    public IReadOnlyList<DiscoverLookupListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public sealed class DiscoverLookupStatsResponse
{
    public int PendingCount { get; set; }
    public int ApprovedTodayCount { get; set; }
    public int RejectedTodayCount { get; set; }
}

public sealed class DiscoverLookupInventoryDto
{
    public JsonElement? Hardware { get; set; }
    public JsonElement? Network { get; set; }
    public JsonElement? OsInfo { get; set; }
    public JsonElement? Security { get; set; }
    public DateTime? CollectedUtc { get; set; }
    public int? Version { get; set; }
}

public sealed class DiscoverLookupApprovalDto
{
    public Guid AdminId { get; set; }
    public string AdminDisplayName { get; set; } = string.Empty;
    public DateTime ApprovedUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class DiscoverLookupRejectionDto
{
    public Guid AdminId { get; set; }
    public string AdminDisplayName { get; set; } = string.Empty;
    public DateTime RejectedUtc { get; set; }
    public string? Reason { get; set; }
}

public sealed class DiscoverLookupDetailDto
{
    public Guid Id { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string DiscoveryType { get; set; } = string.Empty;
    public DiscoverLookupStatus Status { get; set; }
    public DateTime DiscoveredUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public Guid? DeviceId { get; set; }
    public EnrollmentState? DeviceEnrollmentState { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public DiscoverLookupInventoryDto? Inventory { get; set; }
    public DiscoverLookupApprovalDto? Approval { get; set; }
    public DiscoverLookupRejectionDto? Rejection { get; set; }
}

public sealed class ApproveDiscoveryRequest
{
    public Guid? GroupId { get; set; }
    public string? HostName { get; set; }
    public string? Notes { get; set; }
}

public sealed class ApproveDiscoveryResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public EnrollmentState EnrollmentState { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime ApprovedUtc { get; set; }
}

public sealed class RejectDiscoveryRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class DismissDiscoveryRequest
{
    public string? Reason { get; set; }
}

public sealed class BulkApproveDiscoveryRequest
{
    public IReadOnlyList<string> MacAddresses { get; set; } = [];
}

public sealed class BulkApproveDiscoveryItemResult
{
    public string MacAddress { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}

public sealed class BulkApproveDiscoveryResponse
{
    public IReadOnlyList<BulkApproveDiscoveryItemResult> Results { get; set; } = [];
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
}

public sealed class DiscoverLookupOperationResult<T>
{
    public T? Value { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Value is not null;

    public static DiscoverLookupOperationResult<T> Success(T value) =>
        new() { Value = value };

    public static DiscoverLookupOperationResult<T> Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
