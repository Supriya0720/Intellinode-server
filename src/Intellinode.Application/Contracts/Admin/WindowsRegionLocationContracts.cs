namespace Intellinode.Application.Contracts.Admin;

public static class WindowsRegionLocationModuleConstants
{
    public const string ModuleName = "Region And Location Settings";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string DefaultSignalSuffix = "RLS";
    public const int ExcludedWorldGeoId = 39070;
}

public sealed class WindowsRegionLocationExecuteNowRequest
{
    public WindowsRegionLocationTargetRequest Target { get; set; } = new();
    public WindowsRegionLocationSettingsRequest Settings { get; set; } = new();
    public WindowsRegionLocationExecutionRequest Execution { get; set; } = new();
    public WindowsRegionLocationOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionLocationQueueRequest
{
    public WindowsRegionLocationTargetRequest Target { get; set; } = new();
    public WindowsRegionLocationSettingsRequest Settings { get; set; } = new();
    public WindowsRegionLocationExecutionRequest Execution { get; set; } = new();
    public WindowsRegionLocationOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionLocationTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsRegionLocationSettingsRequest
{
    public int GeoId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int LanguageCode { get; set; }
    public string Bcp47Code { get; set; } = string.Empty;
    public string LanguageDescription { get; set; } = string.Empty;
}

public sealed class WindowsRegionLocationExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class WindowsRegionLocationOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsRegionLocationExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionLocationExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsRegionLocationExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsRegionLocationTargetResponse Target { get; set; } = new();
    public WindowsRegionLocationExecutionResponse Execution { get; set; } = new();
    public WindowsRegionLocationLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionLocationQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionLocationQueueData Data { get; set; } = new();
}

public sealed class WindowsRegionLocationQueueData
{
    public Guid TaskId { get; set; }
    public WindowsRegionLocationTargetResponse Target { get; set; } = new();
    public WindowsRegionLocationExecutionResponse Execution { get; set; } = new();
    public WindowsRegionLocationLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionLocationTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsRegionLocationExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsRegionLocationLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsRegionLocationCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionLocationCurrentData Data { get; set; } = new();
}

public sealed class WindowsRegionLocationCurrentData
{
    public WindowsRegionLocationTargetResponse Target { get; set; } = new();
    public WindowsRegionLocationCurrentSettingsDto Settings { get; set; } = new();
    public WindowsRegionLocationCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsRegionLocationCurrentSettingsDto
{
    public int GeoId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int LanguageCode { get; set; }
    public string Bcp47Code { get; set; } = string.Empty;
    public string LanguageDescription { get; set; } = string.Empty;
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsRegionLocationCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsRegionLocationExecuteNowResult
{
    public WindowsRegionLocationExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionLocationExecuteNowResult Success(WindowsRegionLocationExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsRegionLocationExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionLocationQueueResult
{
    public WindowsRegionLocationQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionLocationQueueResult Success(WindowsRegionLocationQueueResponse response) =>
        new() { Response = response };

    public static WindowsRegionLocationQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionLocationCurrentResult
{
    public WindowsRegionLocationCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionLocationCurrentResult Success(WindowsRegionLocationCurrentResponse response) =>
        new() { Response = response };

    public static WindowsRegionLocationCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionLocationHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsRegionLocationHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionLocationHistoryData Data { get; set; } = new();
}

public sealed class WindowsRegionLocationHistoryData
{
    public WindowsRegionLocationTargetResponse Target { get; set; } = new();
    public List<WindowsRegionLocationHistoryItem> Items { get; set; } = [];
    public WindowsRegionLocationPagination Pagination { get; set; } = new();
}

public sealed class WindowsRegionLocationHistoryItem
{
    public Guid? TaskId { get; set; }
    public int? LegacyTaskId { get; set; }
    public string? ModuleName { get; set; }
    public string? FunctionName { get; set; }
    public string? TaskStatus { get; set; }
    public string? ApplyStatus { get; set; }
    public string? ApplyMode { get; set; }
    public long? SettingsVersion { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class WindowsRegionLocationPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsRegionLocationHistoryResult
{
    public WindowsRegionLocationHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionLocationHistoryResult Success(WindowsRegionLocationHistoryResponse response) =>
        new() { Response = response };

    public static WindowsRegionLocationHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionLocationExecuteNowBulkRequest
{
    public List<WindowsRegionLocationTargetRequest> Targets { get; set; } = [];
    public WindowsRegionLocationSettingsRequest Settings { get; set; } = new();
    public WindowsRegionLocationExecutionRequest Execution { get; set; } = new();
    public WindowsRegionLocationOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionLocationExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsRegionLocationSettingsRequest Settings { get; set; } = new();
    public WindowsRegionLocationExecutionRequest Execution { get; set; } = new();
    public WindowsRegionLocationOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionLocationBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionLocationBulkData Data { get; set; } = new();
}

public sealed class WindowsRegionLocationBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsRegionLocationTargetResult> Results { get; set; } = [];
    public WindowsRegionLocationLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionLocationTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsRegionLocationBulkResult
{
    public WindowsRegionLocationBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionLocationBulkResult Success(WindowsRegionLocationBulkResponse response) =>
        new() { Response = response };

    public static WindowsRegionLocationBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionLocationErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionLocationPayloadRequest
{
    public int GeoId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int LanguageCode { get; set; }
    public string Bcp47Code { get; set; } = string.Empty;
    public string LanguageDescription { get; set; } = string.Empty;
    public int TaskID { get; set; }
    public int AgentAction { get; set; }
}
