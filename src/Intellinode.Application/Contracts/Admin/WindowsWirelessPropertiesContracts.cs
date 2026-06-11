namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// Persisted inner document for FusionX <c>XPWirelessNetworkSecuritySettings</c> (no <c>WinCELinux</c> wrapper).
/// Field names match <c>structXP_Data.cs</c>.
/// </summary>
public sealed class WindowsWirelessPropertiesSettingsDocument
{
    /// <summary>Inner FusionX <c>XPWirelessNetworkSecuritySettings</c> JSON (no WinCELinux wrapper).</summary>
    public string SettingsJson { get; set; } = "{}";
}

/// <summary>
/// Compact task reference stored in <c>device_tasks.function_parameter</c> (ADR-0003 Option B).
/// </summary>
public sealed class WindowsWirelessPropertiesCompactTaskReference
{
    public long SettingsVersion { get; set; }
    public long ProfileKey { get; set; }
}

public static class WindowsWirelessPropertiesModuleConstants
{
    public const string ModuleName = "Wireless Network Security";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string DefaultSignalSuffix = "WNS";
}

public static class WindowsWirelessPropertiesSensitiveFields
{
    public const string NetworkKeyPropertyName = "strNetworkKey";
    public const string PreSharedKeyPropertyName = "strNetworkPPK";
    public const string RedactedValue = "********";
}

public enum WirelessProfileOperation
{
    Add,
    Update,
    Delete
}

public sealed class WindowsWirelessPropertiesTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = "XP";
}

/// <summary>
/// API profile fields mapped to FusionX <c>XPWirelessNetworkSecuritySettings</c> / <c>WindowsWirelessDAC</c>.
/// Auth values: No authentication (Open), Shared, WPA-Enterprise, WPA-Personal, WPA2-Enterprise, WPA2-Personal.
/// </summary>
public sealed class WindowsWirelessPropertiesProfileRequest
{
    /// <summary>FusionX <c>strNetworkSSDIName</c> / <c>SSID_Name</c>.</summary>
    public string Ssid { get; set; } = string.Empty;

    /// <summary>FusionX <c>strNetworkAuthentication</c> / <c>Network_Authentication</c>.</summary>
    public string NetworkAuthentication { get; set; } = string.Empty;

    /// <summary>FusionX <c>strNetworkDataEncr</c> / <c>Data_Encription</c> (e.g. None, AES, TKIP).</summary>
    public string DataEncryption { get; set; } = string.Empty;

    /// <summary>FusionX <c>strNetworkKey</c> / <c>Network_Key</c> (max 100).</summary>
    public string NetworkKey { get; set; } = string.Empty;

    /// <summary>FusionX <c>strNetworkPPK</c> / <c>PPK</c> (max 100).</summary>
    public string PreSharedKey { get; set; } = string.Empty;

    /// <summary>FusionX <c>iNetworkKeyIndex</c> / <c>Key_Index</c> (1–4).</summary>
    public int KeyIndex { get; set; }

    /// <summary>FusionX <c>strNetworkName</c> / <c>Network_Name</c> (max 50).</summary>
    public string NetworkName { get; set; } = string.Empty;

    /// <summary>FusionX <c>strStatus</c> / <c>Status</c> (empty on add/update in FusionX).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>FusionX <c>Conn_Auto_WhenIn_Range</c> / <c>Con_Network_When_inRange</c>.</summary>
    public bool ConnectWhenInRange { get; set; }

    /// <summary>
    /// FusionX <c>Text1</c> from <c>chkConnectnetworkboradcasting</c> — serialized as <c>"true"</c> / <c>"false"</c> string.
    /// </summary>
    public bool ConnectNonBroadcasting { get; set; }

    /// <summary>FusionX <c>Text2</c>.</summary>
    public string Text2 { get; set; } = string.Empty;

    /// <summary>FusionX <c>Text3</c>.</summary>
    public string Text3 { get; set; } = string.Empty;
}

public sealed class WindowsWirelessPropertiesExecuteNowRequest
{
    public WindowsWirelessPropertiesTargetRequest Target { get; set; } = new();
    public WirelessProfileOperation Operation { get; set; } = WirelessProfileOperation.Add;
    public WindowsWirelessPropertiesProfileRequest Profile { get; set; } = new();
    public WindowsWirelessPropertiesExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessPropertiesOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesQueueRequest
{
    public WindowsWirelessPropertiesTargetRequest Target { get; set; } = new();
    public WirelessProfileOperation Operation { get; set; } = WirelessProfileOperation.Add;
    public WindowsWirelessPropertiesProfileRequest Profile { get; set; } = new();
    public WindowsWirelessPropertiesExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessPropertiesOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesDeleteRequest
{
    public WindowsWirelessPropertiesTargetRequest Target { get; set; } = new();
    /// <summary>SSID to delete. v1: one SSID per task (FusionX parity).</summary>
    public string Ssid { get; set; } = string.Empty;
    public WindowsWirelessPropertiesExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessPropertiesOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class WindowsWirelessPropertiesOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsWirelessPropertiesTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsWirelessPropertiesExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsWirelessPropertiesLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsWirelessPropertiesExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesExecuteNowData
{
    public Guid TaskId { get; set; }
    public long ProfileKey { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public WirelessProfileOperation Operation { get; set; }
    public WindowsWirelessPropertiesTargetResponse Target { get; set; } = new();
    public WindowsWirelessPropertiesExecutionResponse Execution { get; set; } = new();
    public WindowsWirelessPropertiesLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessPropertiesQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesQueueData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesQueueData
{
    public Guid TaskId { get; set; }
    public long ProfileKey { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public WirelessProfileOperation Operation { get; set; }
    public WindowsWirelessPropertiesTargetResponse Target { get; set; } = new();
    public WindowsWirelessPropertiesExecutionResponse Execution { get; set; } = new();
    public WindowsWirelessPropertiesLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessPropertiesDeleteExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesDeleteExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesDeleteExecuteNowData
{
    public Guid TaskId { get; set; }
    public long ProfileKey { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public WindowsWirelessPropertiesTargetResponse Target { get; set; } = new();
    public WindowsWirelessPropertiesExecutionResponse Execution { get; set; } = new();
    public WindowsWirelessPropertiesLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessPropertiesDeleteQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesDeleteQueueData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesDeleteQueueData
{
    public Guid TaskId { get; set; }
    public long ProfileKey { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public WindowsWirelessPropertiesTargetResponse Target { get; set; } = new();
    public WindowsWirelessPropertiesExecutionResponse Execution { get; set; } = new();
    public WindowsWirelessPropertiesLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessPropertiesProfileDto
{
    public long ProfileKey { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsWirelessPropertiesListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesListData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesListData
{
    public WindowsWirelessPropertiesTargetResponse Target { get; set; } = new();
    public List<WindowsWirelessPropertiesProfileDto> Profiles { get; set; } = [];
}

public sealed class WindowsWirelessPropertiesProfileResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesProfileData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesProfileData
{
    public WindowsWirelessPropertiesTargetResponse Target { get; set; } = new();
    public WindowsWirelessPropertiesProfileDto Profile { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsWirelessPropertiesHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesHistoryData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesHistoryData
{
    public WindowsWirelessPropertiesTargetResponse Target { get; set; } = new();
    public List<WindowsWirelessPropertiesHistoryItem> Items { get; set; } = [];
    public WindowsWirelessPropertiesPagination Pagination { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesHistoryItem
{
    public Guid? TaskId { get; set; }
    public int? LegacyTaskId { get; set; }
    public string? ModuleName { get; set; }
    public string? FunctionName { get; set; }
    public string? TaskStatus { get; set; }
    public string? ApplyStatus { get; set; }
    public string? ApplyMode { get; set; }
    public long? SettingsVersion { get; set; }
    public long? ProfileKey { get; set; }
    public string? Ssid { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class WindowsWirelessPropertiesPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsWirelessPropertiesErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessPropertiesExecuteNowResult
{
    public WindowsWirelessPropertiesExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesExecuteNowResult Success(WindowsWirelessPropertiesExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessPropertiesQueueResult
{
    public WindowsWirelessPropertiesQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesQueueResult Success(WindowsWirelessPropertiesQueueResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessPropertiesDeleteExecuteNowResult
{
    public WindowsWirelessPropertiesDeleteExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesDeleteExecuteNowResult Success(WindowsWirelessPropertiesDeleteExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesDeleteExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessPropertiesDeleteQueueResult
{
    public WindowsWirelessPropertiesDeleteQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesDeleteQueueResult Success(WindowsWirelessPropertiesDeleteQueueResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesDeleteQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessPropertiesListResult
{
    public WindowsWirelessPropertiesListResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesListResult Success(WindowsWirelessPropertiesListResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesListResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessPropertiesProfileResult
{
    public WindowsWirelessPropertiesProfileResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesProfileResult Success(WindowsWirelessPropertiesProfileResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesProfileResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessPropertiesHistoryResult
{
    public WindowsWirelessPropertiesHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesHistoryResult Success(WindowsWirelessPropertiesHistoryResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessPropertiesExecuteNowBulkRequest
{
    public List<WindowsWirelessPropertiesTargetRequest> Targets { get; set; } = [];
    public WirelessProfileOperation Operation { get; set; }
    public WindowsWirelessPropertiesProfileRequest Profile { get; set; } = new();
    public WindowsWirelessPropertiesExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessPropertiesOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WirelessProfileOperation Operation { get; set; }
    public WindowsWirelessPropertiesProfileRequest Profile { get; set; } = new();
    public WindowsWirelessPropertiesExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessPropertiesOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesDeleteExecuteNowBulkRequest
{
    public List<WindowsWirelessPropertiesTargetRequest> Targets { get; set; } = [];
    public string Ssid { get; set; } = string.Empty;
    public WindowsWirelessPropertiesExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessPropertiesOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesDeleteExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public string Ssid { get; set; } = string.Empty;
    public WindowsWirelessPropertiesExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessPropertiesOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessPropertiesBulkData Data { get; set; } = new();
}

public sealed class WindowsWirelessPropertiesBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsWirelessPropertiesTargetResult> Results { get; set; } = [];
    public WindowsWirelessPropertiesLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessPropertiesTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Ssid { get; set; }
    public long? ProfileKey { get; set; }
}

public sealed class WindowsWirelessPropertiesBulkResult
{
    public WindowsWirelessPropertiesBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessPropertiesBulkResult Success(WindowsWirelessPropertiesBulkResponse response) =>
        new() { Response = response };

    public static WindowsWirelessPropertiesBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
