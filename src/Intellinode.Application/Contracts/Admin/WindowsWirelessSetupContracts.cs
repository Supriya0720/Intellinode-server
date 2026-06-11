namespace Intellinode.Application.Contracts.Admin;

public static class WindowsWirelessSetupModuleConstants
{
    public const string ModuleName = "Wireless";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string DefaultSignalSuffix = "W";
}

public sealed class WindowsWirelessSetupCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessSetupCurrentData Data { get; set; } = new();
}

public sealed class WindowsWirelessSetupCurrentData
{
    public WindowsWirelessSetupTargetResponse Target { get; set; } = new();
    public WindowsWirelessSetupReportedDto Reported { get; set; } = new();
    public WindowsWirelessSetupDesiredDto Desired { get; set; } = new();
    public WindowsWirelessSetupCompatDto Compat { get; set; } = new();
}

public sealed class WindowsWirelessSetupTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

/// <summary>
/// v1 stub: wireless adapter inventory is deferred; reported fields are empty until a future inventory PR.
/// </summary>
public sealed class WindowsWirelessSetupReportedDto
{
    public bool IsAvailable { get; set; }
    public bool IsDhcp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
}

public sealed class WindowsWirelessSetupDesiredDto
{
    public bool IsDhcp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsWirelessSetupCompatDto
{
    public string Source { get; set; } = "none";
    public string ModuleName { get; set; } = WindowsWirelessSetupModuleConstants.ModuleName;
    public string SignalSuffix { get; set; } = WindowsWirelessSetupModuleConstants.DefaultSignalSuffix;
}

public sealed class WindowsWirelessSetupCurrentResult
{
    public WindowsWirelessSetupCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessSetupCurrentResult Success(WindowsWirelessSetupCurrentResponse response) =>
        new() { Response = response };

    public static WindowsWirelessSetupCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessSetupErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessSetupExecuteNowRequest
{
    public WindowsWirelessSetupTargetRequest Target { get; set; } = new();
    public WindowsWirelessSetupSettingsRequest Settings { get; set; } = new();
    public WindowsWirelessSetupExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessSetupTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsWirelessSetupSettingsRequest
{
    public bool IsDhcp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
}

public sealed class WindowsWirelessSetupExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class WindowsWirelessSetupOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsWirelessSetupExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessSetupExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsWirelessSetupExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsWirelessSetupTargetResponse Target { get; set; } = new();
    public WindowsWirelessSetupExecutionResponse Execution { get; set; } = new();
    public WindowsWirelessSetupLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessSetupExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsWirelessSetupLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsWirelessSetupExecuteNowResult
{
    public WindowsWirelessSetupExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessSetupExecuteNowResult Success(WindowsWirelessSetupExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsWirelessSetupExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessSetupQueueRequest
{
    public WindowsWirelessSetupTargetRequest Target { get; set; } = new();
    public WindowsWirelessSetupSettingsRequest Settings { get; set; } = new();
    public WindowsWirelessSetupExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessSetupQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessSetupQueueData Data { get; set; } = new();
}

public sealed class WindowsWirelessSetupQueueData
{
    public Guid TaskId { get; set; }
    public WindowsWirelessSetupTargetResponse Target { get; set; } = new();
    public WindowsWirelessSetupExecutionResponse Execution { get; set; } = new();
    public WindowsWirelessSetupLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessSetupQueueResult
{
    public WindowsWirelessSetupQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessSetupQueueResult Success(WindowsWirelessSetupQueueResponse response) =>
        new() { Response = response };

    public static WindowsWirelessSetupQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessSetupHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsWirelessSetupHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessSetupHistoryData Data { get; set; } = new();
}

public sealed class WindowsWirelessSetupHistoryData
{
    public WindowsWirelessSetupTargetResponse Target { get; set; } = new();
    public List<WindowsWirelessSetupHistoryItem> Items { get; set; } = [];
    public WindowsWirelessSetupHistoryPagination Pagination { get; set; } = new();
}

public sealed class WindowsWirelessSetupHistoryItem
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

public sealed class WindowsWirelessSetupHistoryPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsWirelessSetupHistoryResult
{
    public WindowsWirelessSetupHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessSetupHistoryResult Success(WindowsWirelessSetupHistoryResponse response) =>
        new() { Response = response };

    public static WindowsWirelessSetupHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessSetupExecuteNowBulkRequest
{
    public List<WindowsWirelessSetupTargetRequest> Targets { get; set; } = [];
    public WindowsWirelessSetupSettingsRequest Settings { get; set; } = new();
    public WindowsWirelessSetupExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessSetupExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsWirelessSetupSettingsRequest Settings { get; set; } = new();
    public WindowsWirelessSetupExecutionRequest Execution { get; set; } = new();
    public WindowsWirelessSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWirelessSetupBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWirelessSetupBulkData Data { get; set; } = new();
}

public sealed class WindowsWirelessSetupBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsWirelessSetupTargetResult> Results { get; set; } = [];
    public WindowsWirelessSetupLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWirelessSetupTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? AppliedIpAddress { get; set; }
}

public sealed class WindowsWirelessSetupBulkResult
{
    public WindowsWirelessSetupBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWirelessSetupBulkResult Success(WindowsWirelessSetupBulkResponse response) =>
        new() { Response = response };

    public static WindowsWirelessSetupBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWirelessSetupPayloadRequest
{
    public string MacAddr { get; set; } = string.Empty;
    public bool Dhcp { get; set; }
    public string IpAddr { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PriDns { get; set; } = string.Empty;
    public string SecDns { get; set; } = string.Empty;
    public string PriWns { get; set; } = string.Empty;
    public string SecWns { get; set; } = string.Empty;
    public int TaskID { get; set; }
    public int AgentAction { get; set; }
}
