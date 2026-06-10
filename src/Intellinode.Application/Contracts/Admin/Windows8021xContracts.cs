namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// Persisted inner document for FusionX <c>Windows_802_1x</c> (no <c>WinCELinux</c> wrapper).
/// Field names match <c>structXP_Data.cs</c>: <c>blEnable802_Authentication</c>, <c>str_Authentication</c>,
/// <c>cSSID</c>, <c>objTrusted_Root_Certificate_Authorities_PEAP_TLS</c>, EKU arrays, etc.
/// </summary>
public sealed class Windows8021xSettingsDocument
{
    /// <summary>Raw JSON using FusionX struct field names (not camelCased).</summary>
    public string SettingsJson { get; set; } = "{}";
}

/// <summary>
/// Compact task reference stored in <c>device_tasks.function_parameter</c> (ADR-0001 Option A).
/// </summary>
public sealed class Windows8021xCompactTaskReference
{
    public long SettingsVersion { get; set; }
}

public static class Windows8021xModuleConstants
{
    public const string ModuleName = "Windows_802_1x";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
}

public static class Windows8021xSensitiveFields
{
    public const string PasswordPropertyName = "cPassword";
    public const string RedactedPasswordValue = "********";
}

public sealed class Windows8021xExecuteNowRequest
{
    public Windows8021xTargetRequest Target { get; set; } = new();
    public Windows8021xSettingsRequest Settings { get; set; } = new();
    public Windows8021xExecutionRequest Execution { get; set; } = new();
    public Windows8021xOptionsRequest Options { get; set; } = new();
}

public sealed class Windows8021xQueueRequest
{
    public Windows8021xTargetRequest Target { get; set; } = new();
    public Windows8021xSettingsRequest Settings { get; set; } = new();
    public Windows8021xExecutionRequest Execution { get; set; } = new();
    public Windows8021xOptionsRequest Options { get; set; } = new();
}

public sealed class Windows8021xTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class Windows8021xSettingsRequest
{
    /// <summary>Inner FusionX Windows_802_1x JSON object (not WinCELinux wrapper).</summary>
    public string SettingsJson { get; set; } = "{}";
}

public sealed class Windows8021xExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class Windows8021xOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class Windows8021xExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Windows8021xExecuteNowData Data { get; set; } = new();
}

public sealed class Windows8021xExecuteNowData
{
    public Guid TaskId { get; set; }
    public Windows8021xTargetResponse Target { get; set; } = new();
    public Windows8021xExecutionResponse Execution { get; set; } = new();
    public Windows8021xLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class Windows8021xTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class Windows8021xExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class Windows8021xLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class Windows8021xQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Windows8021xQueueData Data { get; set; } = new();
}

public sealed class Windows8021xQueueData
{
    public Guid TaskId { get; set; }
    public Windows8021xTargetResponse Target { get; set; } = new();
    public Windows8021xExecutionResponse Execution { get; set; } = new();
    public Windows8021xLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class Windows8021xCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Windows8021xCurrentData Data { get; set; } = new();
}

public sealed class Windows8021xCurrentData
{
    public Windows8021xTargetResponse Target { get; set; } = new();
    public Windows8021xCurrentSettingsDto Settings { get; set; } = new();
    public Windows8021xCurrentCompatDto Compat { get; set; } = new();
}

public sealed class Windows8021xCurrentSettingsDto
{
    /// <summary>Inner FusionX document with <c>cPassword</c> redacted.</summary>
    public string SettingsJson { get; set; } = "{}";

    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class Windows8021xCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class Windows8021xExecuteNowResult
{
    public Windows8021xExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static Windows8021xExecuteNowResult Success(Windows8021xExecuteNowResponse response) =>
        new() { Response = response };

    public static Windows8021xExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class Windows8021xQueueResult
{
    public Windows8021xQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static Windows8021xQueueResult Success(Windows8021xQueueResponse response) =>
        new() { Response = response };

    public static Windows8021xQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class Windows8021xCurrentResult
{
    public Windows8021xCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static Windows8021xCurrentResult Success(Windows8021xCurrentResponse response) =>
        new() { Response = response };

    public static Windows8021xCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class Windows8021xErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class Windows8021xHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class Windows8021xHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Windows8021xHistoryData Data { get; set; } = new();
}

public sealed class Windows8021xHistoryData
{
    public Windows8021xTargetResponse Target { get; set; } = new();
    public List<Windows8021xHistoryItem> Items { get; set; } = [];
    public Windows8021xPagination Pagination { get; set; } = new();
}

public sealed class Windows8021xHistoryItem
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

public sealed class Windows8021xPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class Windows8021xHistoryResult
{
    public Windows8021xHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static Windows8021xHistoryResult Success(Windows8021xHistoryResponse response) =>
        new() { Response = response };

    public static Windows8021xHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
