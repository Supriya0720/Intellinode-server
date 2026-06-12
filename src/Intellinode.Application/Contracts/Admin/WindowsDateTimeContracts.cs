namespace Intellinode.Application.Contracts.Admin;

using Intellinode.Domain.Enums;

public static class WindowsDateTimeModuleConstants
{
    public const string DateTimeModuleName = "DateTime";
    public const string TimeZoneModuleName = "TimeZone";
    public const string TimeServerModuleName = "TimeServerSynchro";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string DefaultManualDateTimeSignalSuffix = "DT";
    public const string DefaultTimeZoneSignalSuffix = "TZ";
    public const string DefaultTimeServerSignalSuffix = "TS";
}

public sealed class WindowsDateTimeExecuteNowRequest
{
    public WindowsDateTimeTargetRequest Target { get; set; } = new();
    public WindowsDateTimeSettingsRequest Settings { get; set; } = new();
    public WindowsDateTimeExecutionRequest Execution { get; set; } = new();
    public WindowsDateTimeOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsDateTimeQueueRequest
{
    public WindowsDateTimeTargetRequest Target { get; set; } = new();
    public WindowsDateTimeSettingsRequest Settings { get; set; } = new();
    public WindowsDateTimeExecutionRequest Execution { get; set; } = new();
    public WindowsDateTimeOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsDateTimeTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsDateTimeSettingsRequest
{
    public WindowsDateTimeApplyMode ApplyMode { get; set; }
    public DateOnly? CurrentDateLocal { get; set; }
    public string? CurrentTimeLocal { get; set; }
    public string? TimeZoneDisplay { get; set; }
    public string? WindowsTzKey { get; set; }
    public string? TimeServer { get; set; }
}

public sealed class WindowsDateTimeExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class WindowsDateTimeOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsDateTimeExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsDateTimeExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsDateTimeExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsDateTimeTargetResponse Target { get; set; } = new();
    public WindowsDateTimeExecutionResponse Execution { get; set; } = new();
    public WindowsDateTimeLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsDateTimeQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsDateTimeQueueData Data { get; set; } = new();
}

public sealed class WindowsDateTimeQueueData
{
    public Guid TaskId { get; set; }
    public WindowsDateTimeTargetResponse Target { get; set; } = new();
    public WindowsDateTimeExecutionResponse Execution { get; set; } = new();
    public WindowsDateTimeLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsDateTimeTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsDateTimeExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsDateTimeLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsDateTimeCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsDateTimeCurrentData Data { get; set; } = new();
}

public sealed class WindowsDateTimeCurrentData
{
    public WindowsDateTimeTargetResponse Target { get; set; } = new();
    public WindowsDateTimeCurrentSettingsDto Settings { get; set; } = new();
    public WindowsDateTimeCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsDateTimeCurrentSettingsDto
{
    public WindowsDateTimeApplyMode ApplyMode { get; set; }
    public DateOnly? CurrentDateLocal { get; set; }
    public string? CurrentTimeLocal { get; set; }
    public string? TimeZoneDisplay { get; set; }
    public string? WindowsTzKey { get; set; }
    public string? TimeServer { get; set; }
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsDateTimeCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsDateTimeExecuteNowResult
{
    public WindowsDateTimeExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsDateTimeExecuteNowResult Success(WindowsDateTimeExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsDateTimeExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsDateTimeQueueResult
{
    public WindowsDateTimeQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsDateTimeQueueResult Success(WindowsDateTimeQueueResponse response) =>
        new() { Response = response };

    public static WindowsDateTimeQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsDateTimeCurrentResult
{
    public WindowsDateTimeCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsDateTimeCurrentResult Success(WindowsDateTimeCurrentResponse response) =>
        new() { Response = response };

    public static WindowsDateTimeCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsDateTimeHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsDateTimeHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsDateTimeHistoryData Data { get; set; } = new();
}

public sealed class WindowsDateTimeHistoryData
{
    public WindowsDateTimeTargetResponse Target { get; set; } = new();
    public List<WindowsDateTimeHistoryItem> Items { get; set; } = [];
    public WindowsDateTimePagination Pagination { get; set; } = new();
}

public sealed class WindowsDateTimeHistoryItem
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

public sealed class WindowsDateTimePagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsDateTimeHistoryResult
{
    public WindowsDateTimeHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsDateTimeHistoryResult Success(WindowsDateTimeHistoryResponse response) =>
        new() { Response = response };

    public static WindowsDateTimeHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsDateTimeExecuteNowBulkRequest
{
    public List<WindowsDateTimeTargetRequest> Targets { get; set; } = [];
    public WindowsDateTimeSettingsRequest Settings { get; set; } = new();
    public WindowsDateTimeExecutionRequest Execution { get; set; } = new();
    public WindowsDateTimeOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsDateTimeExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsDateTimeSettingsRequest Settings { get; set; } = new();
    public WindowsDateTimeExecutionRequest Execution { get; set; } = new();
    public WindowsDateTimeOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsDateTimeBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsDateTimeBulkData Data { get; set; } = new();
}

public sealed class WindowsDateTimeBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsDateTimeTargetResult> Results { get; set; } = [];
    public WindowsDateTimeLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsDateTimeTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsDateTimeBulkResult
{
    public WindowsDateTimeBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsDateTimeBulkResult Success(WindowsDateTimeBulkResponse response) =>
        new() { Response = response };

    public static WindowsDateTimeBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsDateTimeErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsDateTimePayloadRequest
{
    public WindowsDateTimeApplyMode ApplyMode { get; set; }
    public DateOnly? CurrentDateLocal { get; set; }
    public TimeOnly? CurrentTimeLocal { get; set; }
    public string TimeZoneDisplay { get; set; } = string.Empty;
    public string WindowsTzKey { get; set; } = string.Empty;
    public string TimeServer { get; set; } = string.Empty;
    public int TaskID { get; set; }
    public int AgentAction { get; set; }
}
