namespace Intellinode.Application.Contracts.Admin;

public sealed class KeyboardExecuteNowRequest
{
    public KeyboardTargetRequest Target { get; set; } = new();
    public KeyboardSettingsRequest Settings { get; set; } = new();
    public KeyboardExecutionRequest Execution { get; set; } = new();
    public KeyboardOptionsRequest Options { get; set; } = new();
}

public sealed class KeyboardTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class KeyboardSettingsRequest
{
    public int Delay { get; set; }
    public int RepeatRate { get; set; }
    public string KeyboardLocale { get; set; } = string.Empty;
    public bool ReplaceExistingKeyboard { get; set; }
}

public sealed class KeyboardExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class KeyboardOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class KeyboardExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public KeyboardExecuteNowData Data { get; set; } = new();
}

public sealed class KeyboardExecuteNowData
{
    public Guid TaskId { get; set; }
    public KeyboardTargetResponse Target { get; set; } = new();
    public KeyboardExecutionResponse Execution { get; set; } = new();
    public KeyboardLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class KeyboardTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class KeyboardExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class KeyboardLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class KeyboardCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public KeyboardCurrentData Data { get; set; } = new();
}

public sealed class KeyboardCurrentData
{
    public KeyboardTargetResponse Target { get; set; } = new();
    public KeyboardCurrentSettingsDto Settings { get; set; } = new();
    public KeyboardCurrentCompatDto Compat { get; set; } = new();
}

public sealed class KeyboardCurrentSettingsDto
{
    public int Delay { get; set; }
    public int RepeatRate { get; set; }
    public string KeyboardLocale { get; set; } = string.Empty;
    public bool ReplaceExistingKeyboard { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class KeyboardCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class KeyboardQueueRequest
{
    public KeyboardTargetRequest Target { get; set; } = new();
    public KeyboardSettingsRequest Settings { get; set; } = new();
    public KeyboardExecutionRequest Execution { get; set; } = new();
    public KeyboardOptionsRequest Options { get; set; } = new();
}

public sealed class KeyboardQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public KeyboardQueueData Data { get; set; } = new();
}

public sealed class KeyboardQueueData
{
    public Guid TaskId { get; set; }
    public KeyboardTargetResponse Target { get; set; } = new();
    public KeyboardExecutionResponse Execution { get; set; } = new();
    public KeyboardLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class KeyboardExecuteNowResult
{
    public KeyboardExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static KeyboardExecuteNowResult Success(KeyboardExecuteNowResponse response) =>
        new() { Response = response };

    public static KeyboardExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class KeyboardQueueResult
{
    public KeyboardQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static KeyboardQueueResult Success(KeyboardQueueResponse response) =>
        new() { Response = response };

    public static KeyboardQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class KeyboardCurrentResult
{
    public KeyboardCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static KeyboardCurrentResult Success(KeyboardCurrentResponse response) =>
        new() { Response = response };

    public static KeyboardCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class KeyboardErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class KeyboardHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class KeyboardHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public KeyboardHistoryData Data { get; set; } = new();
}

public sealed class KeyboardHistoryData
{
    public KeyboardTargetResponse Target { get; set; } = new();
    public List<KeyboardHistoryItem> Items { get; set; } = [];
    public KeyboardPagination Pagination { get; set; } = new();
}

public sealed class KeyboardHistoryItem
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

public sealed class KeyboardPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class KeyboardHistoryResult
{
    public KeyboardHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static KeyboardHistoryResult Success(KeyboardHistoryResponse response) =>
        new() { Response = response };

    public static KeyboardHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
