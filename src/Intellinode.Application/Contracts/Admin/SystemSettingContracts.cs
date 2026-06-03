using Intellinode.Domain.Enums;

namespace Intellinode.Application.Contracts.Admin;

public sealed class SystemSettingExecuteNowRequest
{
    public SystemSettingTargetRequest Target { get; set; } = new();
    public SystemSettingRemoteSettingsRequest Settings { get; set; } = new();
    public SystemSettingExecutionRequest Execution { get; set; } = new();
    public SystemSettingOptionsRequest Options { get; set; } = new();
}

public sealed class SystemSettingExecuteNowBulkRequest
{
    public List<SystemSettingTargetRequest> Targets { get; set; } = [];
    public SystemSettingRemoteSettingsRequest Settings { get; set; } = new();
    public SystemSettingExecutionRequest Execution { get; set; } = new();
    public SystemSettingOptionsRequest Options { get; set; } = new();
}

public sealed class SystemSettingQueueRequest
{
    public SystemSettingTargetRequest Target { get; set; } = new();
    public SystemSettingRemoteSettingsRequest Settings { get; set; } = new();
    public SystemSettingExecutionRequest Execution { get; set; } = new();
    public SystemSettingOptionsRequest Options { get; set; } = new();
}

public sealed class SystemSettingTemplateQueueRequest
{
    public SystemSettingTargetRequest Target { get; set; } = new();
    public SystemSettingRemoteSettingsRequest Settings { get; set; } = new();
    public SystemSettingExecutionRequest Execution { get; set; } = new();
    public SystemSettingOptionsRequest Options { get; set; } = new();
}

public sealed class SystemSettingTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class SystemSettingRemoteSettingsRequest
{
    public string ServerIpOrHost { get; set; } = string.Empty;
    public int PortNo { get; set; }
    public int HeartbeatIntervalSeconds { get; set; }
    public CommunicationType CommunicationType { get; set; } = CommunicationType.HTTPS;
    public bool ClientStatus { get; set; } = true;
    public string? GroupName { get; set; }
    public string? HostName { get; set; }
}

public sealed class SystemSettingExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public int ExpiryDurationSeconds { get; set; } = 60;
    public string ModuleType { get; set; } = "SetRemoteSettings";
    public string ModuleName { get; set; } = string.Empty;
    public string Operation { get; set; } = "Update";
    public string Status { get; set; } = "Pending";
    public string ScheduleType { get; set; } = "InstantApply";
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}

public sealed class SystemSettingOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class SystemSettingExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SystemSettingExecuteNowData Data { get; set; } = new();
}

public sealed class SystemSettingBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SystemSettingBulkData Data { get; set; } = new();
}

public sealed class SystemSettingQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SystemSettingQueueData Data { get; set; } = new();
}

public sealed class SystemSettingExecuteNowData
{
    public Guid TaskId { get; set; }
    public SystemSettingTargetResponse Target { get; set; } = new();
    public SystemSettingExecutionResponse Execution { get; set; } = new();
    public SystemSettingLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class SystemSettingBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<SystemSettingTargetResult> Results { get; set; } = [];
    public SystemSettingLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class SystemSettingQueueData
{
    public Guid TaskId { get; set; }
    public SystemSettingTargetResponse Target { get; set; } = new();
    public SystemSettingExecutionResponse Execution { get; set; } = new();
    public SystemSettingTemplateInfo? Template { get; set; }
    public SystemSettingLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class SystemSettingTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class SystemSettingExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class SystemSettingTemplateInfo
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}

public sealed class SystemSettingTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class SystemSettingLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class SystemSettingExecuteNowResult
{
    public SystemSettingExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static SystemSettingExecuteNowResult Success(SystemSettingExecuteNowResponse response) =>
        new() { Response = response };

    public static SystemSettingExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class SystemSettingBulkResult
{
    public SystemSettingBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static SystemSettingBulkResult Success(SystemSettingBulkResponse response) =>
        new() { Response = response };

    public static SystemSettingBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class SystemSettingQueueResult
{
    public SystemSettingQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static SystemSettingQueueResult Success(SystemSettingQueueResponse response) =>
        new() { Response = response };

    public static SystemSettingQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class SystemSettingErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class SystemSettingCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SystemSettingCurrentData Data { get; set; } = new();
}

public sealed class SystemSettingCurrentData
{
    public SystemSettingTargetResponse Target { get; set; } = new();
    public SystemSettingCurrentSettingsDto Settings { get; set; } = new();
    public SystemSettingCurrentCompatDto Compat { get; set; } = new();
}

public sealed class SystemSettingCurrentSettingsDto
{
    public string ServerIpOrHost { get; set; } = string.Empty;
    public int PortNo { get; set; }
    public int HeartbeatIntervalSeconds { get; set; }
    public string CommunicationType { get; set; } = string.Empty;
    public bool ClientStatus { get; set; }
    public string? GroupName { get; set; }
    public string? HostName { get; set; }
    public bool ApplyOnReboot { get; set; }
    public bool PendingApply { get; set; }
    public long SettingsVersion { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
}

public sealed class SystemSettingCurrentCompatDto
{
    public string Source { get; set; } = string.Empty;
    public bool LegacySummaryAvailable { get; set; }
}

public sealed class SystemSettingHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class SystemSettingHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SystemSettingHistoryData Data { get; set; } = new();
}

public sealed class SystemSettingHistoryData
{
    public SystemSettingTargetResponse Target { get; set; } = new();
    public List<SystemSettingHistoryItem> Items { get; set; } = [];
    public SystemSettingPagination Pagination { get; set; } = new();
}

public sealed class SystemSettingHistoryItem
{
    public Guid? TaskId { get; set; }
    public int? LegacyTaskId { get; set; }
    public string? ModuleName { get; set; }
    public string? FunctionName { get; set; }
    public string? Status { get; set; }
    public long? SettingsVersion { get; set; }
    public string? ApplyStatus { get; set; }
    public string? ApplyMode { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public sealed class SystemSettingPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class SystemSettingCurrentResult
{
    public SystemSettingCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static SystemSettingCurrentResult Success(SystemSettingCurrentResponse response) =>
        new() { Response = response };

    public static SystemSettingCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class SystemSettingHistoryResult
{
    public SystemSettingHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static SystemSettingHistoryResult Success(SystemSettingHistoryResponse response) =>
        new() { Response = response };

    public static SystemSettingHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
