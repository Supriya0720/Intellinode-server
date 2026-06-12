namespace Intellinode.Application.Contracts.Admin;

public static class WindowsRegionalFormatModuleConstants
{
    public const string ModuleName = "Regional Settings";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string DefaultSignalSuffix = "RS";
}

public sealed class WindowsRegionalFormatExecuteNowRequest
{
    public WindowsRegionalFormatTargetRequest Target { get; set; } = new();
    public WindowsRegionalFormatSettingsRequest Settings { get; set; } = new();
    public WindowsRegionalFormatExecutionRequest Execution { get; set; } = new();
    public WindowsRegionalFormatOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionalFormatQueueRequest
{
    public WindowsRegionalFormatTargetRequest Target { get; set; } = new();
    public WindowsRegionalFormatSettingsRequest Settings { get; set; } = new();
    public WindowsRegionalFormatExecutionRequest Execution { get; set; } = new();
    public WindowsRegionalFormatOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionalFormatTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsRegionalFormatSettingsRequest
{
    public string TimeFormat { get; set; } = string.Empty;
    public string TimeSeparator { get; set; } = string.Empty;
    public string AmSymbol { get; set; } = string.Empty;
    public string PmSymbol { get; set; } = string.Empty;
    public string ShortDateFormat { get; set; } = string.Empty;
    public string DateSeparator { get; set; } = string.Empty;
    public string LongDateFormat { get; set; } = string.Empty;
    public string ShortDateSample { get; set; } = string.Empty;
    public string LongDateSample { get; set; } = string.Empty;
    public string? TimeSample { get; set; }
}

public sealed class WindowsRegionalFormatExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class WindowsRegionalFormatOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsRegionalFormatExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionalFormatExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsRegionalFormatExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsRegionalFormatTargetResponse Target { get; set; } = new();
    public WindowsRegionalFormatExecutionResponse Execution { get; set; } = new();
    public WindowsRegionalFormatLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionalFormatQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionalFormatQueueData Data { get; set; } = new();
}

public sealed class WindowsRegionalFormatQueueData
{
    public Guid TaskId { get; set; }
    public WindowsRegionalFormatTargetResponse Target { get; set; } = new();
    public WindowsRegionalFormatExecutionResponse Execution { get; set; } = new();
    public WindowsRegionalFormatLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionalFormatTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsRegionalFormatExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsRegionalFormatLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsRegionalFormatCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionalFormatCurrentData Data { get; set; } = new();
}

public sealed class WindowsRegionalFormatCurrentData
{
    public WindowsRegionalFormatTargetResponse Target { get; set; } = new();
    public WindowsRegionalFormatCurrentSettingsDto Settings { get; set; } = new();
    public WindowsRegionalFormatCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsRegionalFormatCurrentSettingsDto
{
    public string TimeFormat { get; set; } = string.Empty;
    public string TimeSeparator { get; set; } = string.Empty;
    public string AmSymbol { get; set; } = string.Empty;
    public string PmSymbol { get; set; } = string.Empty;
    public string ShortDateFormat { get; set; } = string.Empty;
    public string DateSeparator { get; set; } = string.Empty;
    public string LongDateFormat { get; set; } = string.Empty;
    public string ShortDateSample { get; set; } = string.Empty;
    public string LongDateSample { get; set; } = string.Empty;
    public string? TimeSample { get; set; }
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsRegionalFormatCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsRegionalFormatExecuteNowResult
{
    public WindowsRegionalFormatExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionalFormatExecuteNowResult Success(WindowsRegionalFormatExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsRegionalFormatExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionalFormatQueueResult
{
    public WindowsRegionalFormatQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionalFormatQueueResult Success(WindowsRegionalFormatQueueResponse response) =>
        new() { Response = response };

    public static WindowsRegionalFormatQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionalFormatCurrentResult
{
    public WindowsRegionalFormatCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionalFormatCurrentResult Success(WindowsRegionalFormatCurrentResponse response) =>
        new() { Response = response };

    public static WindowsRegionalFormatCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionalFormatHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsRegionalFormatHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionalFormatHistoryData Data { get; set; } = new();
}

public sealed class WindowsRegionalFormatHistoryData
{
    public WindowsRegionalFormatTargetResponse Target { get; set; } = new();
    public List<WindowsRegionalFormatHistoryItem> Items { get; set; } = [];
    public WindowsRegionalFormatPagination Pagination { get; set; } = new();
}

public sealed class WindowsRegionalFormatHistoryItem
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

public sealed class WindowsRegionalFormatPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsRegionalFormatHistoryResult
{
    public WindowsRegionalFormatHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionalFormatHistoryResult Success(WindowsRegionalFormatHistoryResponse response) =>
        new() { Response = response };

    public static WindowsRegionalFormatHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionalFormatExecuteNowBulkRequest
{
    public List<WindowsRegionalFormatTargetRequest> Targets { get; set; } = [];
    public WindowsRegionalFormatSettingsRequest Settings { get; set; } = new();
    public WindowsRegionalFormatExecutionRequest Execution { get; set; } = new();
    public WindowsRegionalFormatOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionalFormatExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsRegionalFormatSettingsRequest Settings { get; set; } = new();
    public WindowsRegionalFormatExecutionRequest Execution { get; set; } = new();
    public WindowsRegionalFormatOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsRegionalFormatBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsRegionalFormatBulkData Data { get; set; } = new();
}

public sealed class WindowsRegionalFormatBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsRegionalFormatTargetResult> Results { get; set; } = [];
    public WindowsRegionalFormatLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionalFormatTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsRegionalFormatBulkResult
{
    public WindowsRegionalFormatBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsRegionalFormatBulkResult Success(WindowsRegionalFormatBulkResponse response) =>
        new() { Response = response };

    public static WindowsRegionalFormatBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsRegionalFormatErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsRegionalFormatPayloadRequest
{
    public string TimeFormat { get; set; } = string.Empty;
    public string TimeSeparator { get; set; } = string.Empty;
    public string AmSymbol { get; set; } = string.Empty;
    public string PmSymbol { get; set; } = string.Empty;
    public string ShortDateFormat { get; set; } = string.Empty;
    public string DateSeparator { get; set; } = string.Empty;
    public string LongDateFormat { get; set; } = string.Empty;
    public string ShortDateSample { get; set; } = string.Empty;
    public string LongDateSample { get; set; } = string.Empty;
    public int TaskID { get; set; }
    public int AgentAction { get; set; }
}

public sealed class RegionalFormatPresetsResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = "Format presets fetched successfully.";
    public RegionalFormatPresetsData Data { get; set; } = new();
}

public sealed class RegionalFormatPresetsData
{
    public IReadOnlyList<string> ShortDateFormats { get; init; } = [];
    public IReadOnlyList<string> LongDateFormats { get; init; } = [];
    public IReadOnlyList<string> TimeFormats { get; init; } = [];
    public IReadOnlyList<string> DateSeparators { get; init; } = [];
    public IReadOnlyList<string> TimeSeparators { get; init; } = [];
}
