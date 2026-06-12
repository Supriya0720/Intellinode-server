namespace Intellinode.Application.Contracts.Admin;

public static class WindowsPowerManagementModuleConstants
{
    public const string ModuleName = "Power Management Settings";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string TemplateQueueFunctionName = "QueueTemplate";
    public const string DefaultSignalSuffix = "PMO";
    public const int MaxFunctionParameterLength = 512;

    public static string MapApplyMode(string? functionName)
    {
        if (string.Equals(functionName, InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, TemplateQueueFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "template";
        }

        return "queued";
    }

    public static bool IsQueuedApplyFunctionName(string? functionName) =>
        string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(functionName, TemplateQueueFunctionName, StringComparison.OrdinalIgnoreCase);
}

public sealed class WindowsPowerManagementCompactTaskReference
{
    public long SettingsVersion { get; set; }
    public string? PlanName { get; set; }
}

public sealed class WindowsPowerManagementSettingValue
{
    public string SettingName { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
}

public sealed class WindowsPowerManagementOptionGroup
{
    public string OptionName { get; set; } = string.Empty;
    public List<WindowsPowerManagementSettingValue> Settings { get; set; } = [];
}

/// <summary>
/// Inner FusionX <c>XPPowerManagement</c> document (without WinCELinux wrapper).
/// </summary>
public sealed class WindowsPowerManagementSettingsJson
{
    public string StrPowerSchemaName { get; set; } = "Balanced";
    public bool BlIsActive { get; set; } = true;
    public List<WindowsPowerManagementOptionGroup> ObjPowerOptions { get; set; } = [];
    public string Operation { get; set; } = "Update";
    public string Index { get; set; } = "1";
}

public sealed class WindowsPowerManagementPayloadRequest
{
    public string SettingsJson { get; set; } = "{}";
    public long LegacyTaskId { get; set; }
    public int AgentAction { get; set; }
}

public sealed class WindowsPowerManagementBasicSettingsRequest
{
    public string PlanName { get; set; } = "Balanced";
    public bool IsActive { get; set; } = true;
    public string Operation { get; set; } = "Update";
    public string Index { get; set; } = "1";
    public List<WindowsPowerManagementOptionGroup> OptionGroups { get; set; } = [];
}

public sealed class WindowsPowerManagementSettingsRequest
{
    public string PlanName { get; set; } = "Balanced";
    public bool IsActive { get; set; } = true;
    public string? DisplayTimeoutText { get; set; }
    public string? SleepTimeoutText { get; set; }
    public string? HardDiskTimeoutText { get; set; }
    public string? PowerButtonAction { get; set; }
    public string? SleepButtonAction { get; set; }
    public string? SystemStandbyTimeoutText { get; set; }
    public List<WindowsPowerManagementOptionGroup>? OptionGroups { get; set; }
}

public sealed class WindowsPowerManagementExecuteNowRequest
{
    public WindowsPowerManagementTargetRequest Target { get; set; } = new();
    public WindowsPowerManagementSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementQueueRequest
{
    public WindowsPowerManagementTargetRequest Target { get; set; } = new();
    public WindowsPowerManagementSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementTemplateQueueRequest
{
    public WindowsPowerManagementTargetRequest Target { get; set; } = new();
    public WindowsPowerManagementSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsPowerManagementExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}

public sealed class WindowsPowerManagementOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsPowerManagementExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsPowerManagementExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsPowerManagementExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsPowerManagementTargetResponse Target { get; set; } = new();
    public WindowsPowerManagementExecutionResponse Execution { get; set; } = new();
    public WindowsPowerManagementLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsPowerManagementQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsPowerManagementQueueData Data { get; set; } = new();
}

public sealed class WindowsPowerManagementQueueData
{
    public Guid TaskId { get; set; }
    public WindowsPowerManagementTargetResponse Target { get; set; } = new();
    public WindowsPowerManagementExecutionResponse Execution { get; set; } = new();
    public WindowsPowerManagementTemplateInfo? Template { get; set; }
    public WindowsPowerManagementLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsPowerManagementTemplateInfo
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}

public sealed class WindowsPowerManagementTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsPowerManagementExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsPowerManagementLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsPowerManagementCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsPowerManagementCurrentData Data { get; set; } = new();
}

public sealed class WindowsPowerManagementCurrentData
{
    public WindowsPowerManagementTargetResponse Target { get; set; } = new();
    public WindowsPowerManagementCurrentSettingsDto Settings { get; set; } = new();
    public WindowsPowerManagementCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsPowerManagementCurrentSettingsDto
{
    public string PlanName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? DisplayTimeoutText { get; set; }
    public string? SleepTimeoutText { get; set; }
    public string? HardDiskTimeoutText { get; set; }
    public string? PowerButtonAction { get; set; }
    public string? SleepButtonAction { get; set; }
    public string? SystemStandbyTimeoutText { get; set; }
    public List<WindowsPowerManagementOptionGroup> OptionGroups { get; set; } = [];
    public List<WindowsPowerManagementOptionGroup> AdvancedOptionGroups { get; set; } = [];
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsPowerManagementCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsPowerManagementExecuteNowResult
{
    public WindowsPowerManagementExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsPowerManagementExecuteNowResult Success(WindowsPowerManagementExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsPowerManagementExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsPowerManagementQueueResult
{
    public WindowsPowerManagementQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsPowerManagementQueueResult Success(WindowsPowerManagementQueueResponse response) =>
        new() { Response = response };

    public static WindowsPowerManagementQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsPowerManagementCurrentResult
{
    public WindowsPowerManagementCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsPowerManagementCurrentResult Success(WindowsPowerManagementCurrentResponse response) =>
        new() { Response = response };

    public static WindowsPowerManagementCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsPowerManagementHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsPowerManagementHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsPowerManagementHistoryData Data { get; set; } = new();
}

public sealed class WindowsPowerManagementHistoryData
{
    public WindowsPowerManagementTargetResponse Target { get; set; } = new();
    public List<WindowsPowerManagementHistoryItem> Items { get; set; } = [];
    public WindowsPowerManagementPagination Pagination { get; set; } = new();
}

public sealed class WindowsPowerManagementHistoryItem
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

public sealed class WindowsPowerManagementPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsPowerManagementHistoryResult
{
    public WindowsPowerManagementHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsPowerManagementHistoryResult Success(WindowsPowerManagementHistoryResponse response) =>
        new() { Response = response };

    public static WindowsPowerManagementHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsPowerManagementExecuteNowBulkRequest
{
    public List<WindowsPowerManagementTargetRequest> Targets { get; set; } = [];
    public WindowsPowerManagementSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsPowerManagementSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsPowerManagementBulkData Data { get; set; } = new();
}

public sealed class WindowsPowerManagementBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsPowerManagementTargetResult> Results { get; set; } = [];
    public WindowsPowerManagementLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsPowerManagementTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsPowerManagementBulkResult
{
    public WindowsPowerManagementBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsPowerManagementBulkResult Success(WindowsPowerManagementBulkResponse response) =>
        new() { Response = response };

    public static WindowsPowerManagementBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsPowerManagementErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsPowerManagementAdvancedSettingsRequest
{
    public string PlanName { get; set; } = "Balanced";
    public bool IsActive { get; set; } = true;
    public List<WindowsPowerManagementOptionGroup> OptionGroups { get; set; } = [];
}

public sealed class WindowsPowerManagementAdvancedExecuteNowRequest
{
    public WindowsPowerManagementTargetRequest Target { get; set; } = new();
    public WindowsPowerManagementAdvancedSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementAdvancedQueueRequest
{
    public WindowsPowerManagementTargetRequest Target { get; set; } = new();
    public WindowsPowerManagementAdvancedSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementAdvancedTemplateQueueRequest
{
    public WindowsPowerManagementTargetRequest Target { get; set; } = new();
    public WindowsPowerManagementAdvancedSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementAdvancedExecuteNowBulkRequest
{
    public List<WindowsPowerManagementTargetRequest> Targets { get; set; } = [];
    public WindowsPowerManagementAdvancedSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsPowerManagementAdvancedExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsPowerManagementAdvancedSettingsRequest Settings { get; set; } = new();
    public WindowsPowerManagementExecutionRequest Execution { get; set; } = new();
    public WindowsPowerManagementOptionsRequest Options { get; set; } = new();
}
