namespace Intellinode.Application.Contracts.Admin;

public sealed class DisplayExecuteNowRequest
{
    public DisplayTargetRequest Target { get; set; } = new();
    public DisplaySettingsRequest Settings { get; set; } = new();
    public DisplayExecutionRequest Execution { get; set; } = new();
    public DisplayOptionsRequest Options { get; set; } = new();
}

public sealed class DisplayTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class DisplaySettingsRequest
{
    public string Resolution { get; set; } = string.Empty;
    public string ColorDepth { get; set; } = string.Empty;
    public string DualDisplayOption { get; set; } = string.Empty;
    public string SecondaryRotation { get; set; } = string.Empty;
}

public sealed class DisplayExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class DisplayOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class DisplayExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DisplayExecuteNowData Data { get; set; } = new();
}

public sealed class DisplayExecuteNowData
{
    public Guid TaskId { get; set; }
    public DisplayTargetResponse Target { get; set; } = new();
    public DisplayExecutionResponse Execution { get; set; } = new();
    public DisplayLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class DisplayTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class DisplayExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class DisplayLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class DisplayCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DisplayCurrentData Data { get; set; } = new();
}

public sealed class DisplayCurrentData
{
    public DisplayTargetResponse Target { get; set; } = new();
    public DisplayCurrentSettingsDto Settings { get; set; } = new();
    public DisplayCurrentCompatDto Compat { get; set; } = new();
}

public sealed class DisplayCurrentSettingsDto
{
    public string Resolution { get; set; } = string.Empty;
    public string ColorDepth { get; set; } = string.Empty;
    public string DualDisplayOption { get; set; } = string.Empty;
    public string SecondaryRotation { get; set; } = string.Empty;
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class DisplayCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class DisplayQueueRequest
{
    public DisplayTargetRequest Target { get; set; } = new();
    public DisplaySettingsRequest Settings { get; set; } = new();
    public DisplayExecutionRequest Execution { get; set; } = new();
    public DisplayOptionsRequest Options { get; set; } = new();
}

public sealed class DisplayQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DisplayQueueData Data { get; set; } = new();
}

public sealed class DisplayQueueData
{
    public Guid TaskId { get; set; }
    public DisplayTargetResponse Target { get; set; } = new();
    public DisplayExecutionResponse Execution { get; set; } = new();
    public DisplayLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class DisplayExecuteNowResult
{
    public DisplayExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static DisplayExecuteNowResult Success(DisplayExecuteNowResponse response) =>
        new() { Response = response };

    public static DisplayExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class DisplayQueueResult
{
    public DisplayQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static DisplayQueueResult Success(DisplayQueueResponse response) =>
        new() { Response = response };

    public static DisplayQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class DisplayCurrentResult
{
    public DisplayCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static DisplayCurrentResult Success(DisplayCurrentResponse response) =>
        new() { Response = response };

    public static DisplayCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class DisplayErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class DisplayHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class DisplayHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DisplayHistoryData Data { get; set; } = new();
}

public sealed class DisplayHistoryData
{
    public DisplayTargetResponse Target { get; set; } = new();
    public List<DisplayHistoryItem> Items { get; set; } = [];
    public DisplayPagination Pagination { get; set; } = new();
}

public sealed class DisplayHistoryItem
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

public sealed class DisplayPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class DisplayHistoryResult
{
    public DisplayHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static DisplayHistoryResult Success(DisplayHistoryResponse response) =>
        new() { Response = response };

    public static DisplayHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
