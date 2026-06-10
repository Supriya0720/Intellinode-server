namespace Intellinode.Application.Contracts.Admin;

using Intellinode.Domain.Enums;

public static class WindowsComputerNameModuleConstants
{
    public const string HostRenameModuleName = "Host Name";
    public const string DomainJoinModuleName = "DomainSettings";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string DefaultSignalSuffix = "CN";
}

public static class WindowsComputerNameSensitiveFields
{
    public const string PasswordPropertyName = "Password";
    public const string RedactedPasswordValue = "********";
}

public sealed class WindowsComputerNameExecuteNowRequest
{
    public WindowsComputerNameTargetRequest Target { get; set; } = new();
    public WindowsComputerNameSettingsRequest Settings { get; set; } = new();
    public WindowsComputerNameExecutionRequest Execution { get; set; } = new();
    public WindowsComputerNameOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsComputerNameQueueRequest
{
    public WindowsComputerNameTargetRequest Target { get; set; } = new();
    public WindowsComputerNameSettingsRequest Settings { get; set; } = new();
    public WindowsComputerNameExecutionRequest Execution { get; set; } = new();
    public WindowsComputerNameOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsComputerNameTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsComputerNameSettingsRequest
{
    public ComputerNameApplyMode ApplyMode { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string WorkGroup { get; set; } = string.Empty;
    public string OrganizationalUnit { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsDomainJoin { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string Postfix { get; set; } = string.Empty;
    public int NoOfChar { get; set; }
    public bool IsMacOrSerial { get; set; }
}

public sealed class WindowsComputerNameExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class WindowsComputerNameOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsComputerNameExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsComputerNameExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsComputerNameExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsComputerNameTargetResponse Target { get; set; } = new();
    public WindowsComputerNameExecutionResponse Execution { get; set; } = new();
    public WindowsComputerNameLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsComputerNameQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsComputerNameQueueData Data { get; set; } = new();
}

public sealed class WindowsComputerNameQueueData
{
    public Guid TaskId { get; set; }
    public WindowsComputerNameTargetResponse Target { get; set; } = new();
    public WindowsComputerNameExecutionResponse Execution { get; set; } = new();
    public WindowsComputerNameLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsComputerNameTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsComputerNameExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsComputerNameLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsComputerNameCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsComputerNameCurrentData Data { get; set; } = new();
}

public sealed class WindowsComputerNameCurrentData
{
    public WindowsComputerNameTargetResponse Target { get; set; } = new();
    public WindowsComputerNameCurrentSettingsDto Settings { get; set; } = new();
    public WindowsComputerNameCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsComputerNameCurrentSettingsDto
{
    public ComputerNameApplyMode ApplyMode { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string WorkGroup { get; set; } = string.Empty;
    public string OrganizationalUnit { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsDomainJoin { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string Postfix { get; set; } = string.Empty;
    public int NoOfChar { get; set; }
    public bool IsMacOrSerial { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsComputerNameCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsComputerNameExecuteNowResult
{
    public WindowsComputerNameExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsComputerNameExecuteNowResult Success(WindowsComputerNameExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsComputerNameExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsComputerNameQueueResult
{
    public WindowsComputerNameQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsComputerNameQueueResult Success(WindowsComputerNameQueueResponse response) =>
        new() { Response = response };

    public static WindowsComputerNameQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsComputerNameCurrentResult
{
    public WindowsComputerNameCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsComputerNameCurrentResult Success(WindowsComputerNameCurrentResponse response) =>
        new() { Response = response };

    public static WindowsComputerNameCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsComputerNameHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsComputerNameHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsComputerNameHistoryData Data { get; set; } = new();
}

public sealed class WindowsComputerNameHistoryData
{
    public WindowsComputerNameTargetResponse Target { get; set; } = new();
    public List<WindowsComputerNameHistoryItem> Items { get; set; } = [];
    public WindowsComputerNamePagination Pagination { get; set; } = new();
}

public sealed class WindowsComputerNameHistoryItem
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

public sealed class WindowsComputerNamePagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsComputerNameHistoryResult
{
    public WindowsComputerNameHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsComputerNameHistoryResult Success(WindowsComputerNameHistoryResponse response) =>
        new() { Response = response };

    public static WindowsComputerNameHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsComputerNameExecuteNowBulkRequest
{
    public List<WindowsComputerNameTargetRequest> Targets { get; set; } = [];
    public WindowsComputerNameSettingsRequest Settings { get; set; } = new();
    public WindowsComputerNameExecutionRequest Execution { get; set; } = new();
    public WindowsComputerNameOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsComputerNameExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsComputerNameSettingsRequest Settings { get; set; } = new();
    public WindowsComputerNameExecutionRequest Execution { get; set; } = new();
    public WindowsComputerNameOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsComputerNameBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsComputerNameBulkData Data { get; set; } = new();
}

public sealed class WindowsComputerNameBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsComputerNameTargetResult> Results { get; set; } = [];
    public WindowsComputerNameLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsComputerNameTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? ResolvedHostName { get; set; }
}

public sealed class WindowsComputerNameBulkResult
{
    public WindowsComputerNameBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsComputerNameBulkResult Success(WindowsComputerNameBulkResponse response) =>
        new() { Response = response };

    public static WindowsComputerNameBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsComputerNameErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsComputerNameHostRenamePayloadRequest
{
    public string MacAddr { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string WorkGroup { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string Postfix { get; set; } = string.Empty;
    public int NoOfChar { get; set; }
    public bool IsMacOrSrNo { get; set; }
    public int TaskID { get; set; }
    public int AgentAction { get; set; }
}

public sealed class WindowsComputerNameDomainJoinPayloadRequest
{
    public string MacAddr { get; set; } = string.Empty;
    public bool IsDomainJoin { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string WorkGroup { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string OrganizationalUnit { get; set; } = string.Empty;
    public int TaskID { get; set; }
    public int AgentAction { get; set; }
}
