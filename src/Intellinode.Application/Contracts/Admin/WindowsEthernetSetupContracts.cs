namespace Intellinode.Application.Contracts.Admin;

public static class WindowsEthernetSetupModuleConstants
{
    public const string ModuleName = "Ethernet";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string DefaultSignalSuffix = "NT&Ethernet";
}

public sealed class WindowsEthernetSetupCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsEthernetSetupCurrentData Data { get; set; } = new();
}

public sealed class WindowsEthernetSetupCurrentData
{
    public WindowsEthernetSetupTargetResponse Target { get; set; } = new();
    public WindowsEthernetSetupReportedDto Reported { get; set; } = new();
    public WindowsEthernetSetupDesiredDto Desired { get; set; } = new();
    public WindowsEthernetSetupCompatDto Compat { get; set; } = new();
}

public sealed class WindowsEthernetSetupTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsEthernetSetupReportedDto
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
    public bool ObtainDnsAutomatically { get; set; }
}

public sealed class WindowsEthernetSetupDesiredDto
{
    public bool IsDhcp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
    public bool ObtainDnsAutomatically { get; set; }
    public string NetworkSpeed { get; set; } = "AutoSelect";
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsEthernetSetupCompatDto
{
    public string Source { get; set; } = "none";
    public string ModuleName { get; set; } = WindowsEthernetSetupModuleConstants.ModuleName;
    public string SignalSuffix { get; set; } = WindowsEthernetSetupModuleConstants.DefaultSignalSuffix;
}

public sealed class WindowsEthernetSetupCurrentResult
{
    public WindowsEthernetSetupCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsEthernetSetupCurrentResult Success(WindowsEthernetSetupCurrentResponse response) =>
        new() { Response = response };

    public static WindowsEthernetSetupCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsEthernetSetupErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsEthernetSetupExecuteNowRequest
{
    public WindowsEthernetSetupTargetRequest Target { get; set; } = new();
    public WindowsEthernetSetupSettingsRequest Settings { get; set; } = new();
    public WindowsEthernetSetupExecutionRequest Execution { get; set; } = new();
    public WindowsEthernetSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsEthernetSetupTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsEthernetSetupSettingsRequest
{
    public bool IsDhcp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
    public bool ObtainDnsAutomatically { get; set; }
    public string NetworkSpeed { get; set; } = "AutoSelect";
}

public sealed class WindowsEthernetSetupExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
}

public sealed class WindowsEthernetSetupOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsEthernetSetupExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsEthernetSetupExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsEthernetSetupExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsEthernetSetupTargetResponse Target { get; set; } = new();
    public WindowsEthernetSetupExecutionResponse Execution { get; set; } = new();
    public WindowsEthernetSetupLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsEthernetSetupExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsEthernetSetupLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsEthernetSetupExecuteNowResult
{
    public WindowsEthernetSetupExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsEthernetSetupExecuteNowResult Success(WindowsEthernetSetupExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsEthernetSetupExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsEthernetSetupQueueRequest
{
    public WindowsEthernetSetupTargetRequest Target { get; set; } = new();
    public WindowsEthernetSetupSettingsRequest Settings { get; set; } = new();
    public WindowsEthernetSetupExecutionRequest Execution { get; set; } = new();
    public WindowsEthernetSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsEthernetSetupQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsEthernetSetupQueueData Data { get; set; } = new();
}

public sealed class WindowsEthernetSetupQueueData
{
    public Guid TaskId { get; set; }
    public WindowsEthernetSetupTargetResponse Target { get; set; } = new();
    public WindowsEthernetSetupExecutionResponse Execution { get; set; } = new();
    public WindowsEthernetSetupLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsEthernetSetupQueueResult
{
    public WindowsEthernetSetupQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsEthernetSetupQueueResult Success(WindowsEthernetSetupQueueResponse response) =>
        new() { Response = response };

    public static WindowsEthernetSetupQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsEthernetSetupHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsEthernetSetupHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsEthernetSetupHistoryData Data { get; set; } = new();
}

public sealed class WindowsEthernetSetupHistoryData
{
    public WindowsEthernetSetupTargetResponse Target { get; set; } = new();
    public List<WindowsEthernetSetupHistoryItem> Items { get; set; } = [];
    public WindowsEthernetSetupHistoryPagination Pagination { get; set; } = new();
}

public sealed class WindowsEthernetSetupHistoryItem
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

public sealed class WindowsEthernetSetupHistoryPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsEthernetSetupHistoryResult
{
    public WindowsEthernetSetupHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsEthernetSetupHistoryResult Success(WindowsEthernetSetupHistoryResponse response) =>
        new() { Response = response };

    public static WindowsEthernetSetupHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsEthernetSetupExecuteNowBulkRequest
{
    public List<WindowsEthernetSetupTargetRequest> Targets { get; set; } = [];
    public WindowsEthernetSetupSettingsRequest Settings { get; set; } = new();
    public WindowsEthernetSetupExecutionRequest Execution { get; set; } = new();
    public WindowsEthernetSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsEthernetSetupExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsEthernetSetupSettingsRequest Settings { get; set; } = new();
    public WindowsEthernetSetupExecutionRequest Execution { get; set; } = new();
    public WindowsEthernetSetupOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsEthernetSetupBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsEthernetSetupBulkData Data { get; set; } = new();
}

public sealed class WindowsEthernetSetupBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsEthernetSetupTargetResult> Results { get; set; } = [];
    public WindowsEthernetSetupLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsEthernetSetupTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? AppliedIpAddress { get; set; }
}

public sealed class WindowsEthernetSetupBulkResult
{
    public WindowsEthernetSetupBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsEthernetSetupBulkResult Success(WindowsEthernetSetupBulkResponse response) =>
        new() { Response = response };

    public static WindowsEthernetSetupBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsEthernetSetupPayloadRequest
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
    public string NetworkSpeed { get; set; } = "AutoSelect";
    public bool IsObtainedDnsAutomatically { get; set; }
    public int TaskID { get; set; }
    public int AgentAction { get; set; }
}
