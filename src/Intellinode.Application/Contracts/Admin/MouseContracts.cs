namespace Intellinode.Application.Contracts.Admin;

public sealed class MouseExecuteNowRequest
{
    public MouseTargetRequest Target { get; set; } = new();
    public MouseSettingsRequest Settings { get; set; } = new();
    public MouseExecutionRequest Execution { get; set; } = new();
    public MouseOptionsRequest Options { get; set; } = new();
}

public sealed class MouseTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class MouseSettingsRequest
{
    public bool Swap { get; set; }
    public int PointerSpeed { get; set; }
    public int DoubleClickSpeed { get; set; }
}

public sealed class MouseExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class MouseOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class MouseExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MouseExecuteNowData Data { get; set; } = new();
}

public sealed class MouseExecuteNowData
{
    public Guid TaskId { get; set; }
    public MouseTargetResponse Target { get; set; } = new();
    public MouseExecutionResponse Execution { get; set; } = new();
    public MouseLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class MouseTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class MouseExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class MouseLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class MouseCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MouseCurrentData Data { get; set; } = new();
}

public sealed class MouseCurrentData
{
    public MouseTargetResponse Target { get; set; } = new();
    public MouseCurrentSettingsDto Settings { get; set; } = new();
    public MouseCurrentCompatDto Compat { get; set; } = new();
}

public sealed class MouseCurrentSettingsDto
{
    public bool Swap { get; set; }
    public int PointerSpeed { get; set; }
    public int DoubleClickSpeed { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class MouseCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class MouseQueueRequest
{
    public MouseTargetRequest Target { get; set; } = new();
    public MouseSettingsRequest Settings { get; set; } = new();
    public MouseExecutionRequest Execution { get; set; } = new();
    public MouseOptionsRequest Options { get; set; } = new();
}

public sealed class MouseQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MouseQueueData Data { get; set; } = new();
}

public sealed class MouseQueueData
{
    public Guid TaskId { get; set; }
    public MouseTargetResponse Target { get; set; } = new();
    public MouseExecutionResponse Execution { get; set; } = new();
    public MouseLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class MouseExecuteNowResult
{
    public MouseExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static MouseExecuteNowResult Success(MouseExecuteNowResponse response) =>
        new() { Response = response };

    public static MouseExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class MouseQueueResult
{
    public MouseQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static MouseQueueResult Success(MouseQueueResponse response) =>
        new() { Response = response };

    public static MouseQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class MouseCurrentResult
{
    public MouseCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static MouseCurrentResult Success(MouseCurrentResponse response) =>
        new() { Response = response };

    public static MouseCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class MouseErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class MouseHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class MouseHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public MouseHistoryData Data { get; set; } = new();
}

public sealed class MouseHistoryData
{
    public MouseTargetResponse Target { get; set; } = new();
    public List<MouseHistoryItem> Items { get; set; } = [];
    public MousePagination Pagination { get; set; } = new();
}

public sealed class MouseHistoryItem
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

public sealed class MousePagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class MouseHistoryResult
{
    public MouseHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static MouseHistoryResult Success(MouseHistoryResponse response) =>
        new() { Response = response };

    public static MouseHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
