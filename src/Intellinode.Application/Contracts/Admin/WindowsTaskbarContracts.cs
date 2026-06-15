namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// FusionX User Settings → Taskbar Properties (ModuleType <c>Taskbar</c>, signal <c>TPR</c>).
/// </summary>
public static class WindowsTaskbarModuleConstants
{
    public const string ModuleName = "Taskbar";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string TemplateQueueFunctionName = "QueueTemplate";
    public const string LiveReadFunctionName = "Get";
    public const string DefaultSignalSuffix = "TPR";
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

        if (string.Equals(functionName, LiveReadFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "live-read";
        }

        return "queued";
    }

    public static bool IsQueuedApplyFunctionName(string? functionName) =>
        string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(functionName, TemplateQueueFunctionName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Input for <see cref="Interfaces.IWindowsTaskbarPayloadBuilder.BuildAgentPayload"/>.
/// Field mapping follows FusionX <c>WinCELinux.XPTaskbarProperties</c>.
/// </summary>
public sealed class WindowsTaskbarPayloadRequest
{
    public bool LockTaskbar { get; set; } = true;
    public bool AutoHideTaskbar { get; set; }
    public bool KeepTaskbarOnTop { get; set; } = true;
    public bool GroupSimilarButtons { get; set; } = true;
    public bool ShowQuickLaunch { get; set; }
    public long TaskId { get; set; }
    public int AgentAction { get; set; }
}

public sealed class WindowsTaskbarTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsTaskbarCurrentSettingsDto
{
    public bool LockTaskbar { get; set; } = true;
    public bool AutoHideTaskbar { get; set; }
    public bool KeepTaskbarOnTop { get; set; } = true;
    public bool GroupSimilarButtons { get; set; } = true;
    public bool ShowQuickLaunch { get; set; }
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }

    public static WindowsTaskbarCurrentSettingsDto CreateFusionXDefaults() =>
        new()
        {
            LockTaskbar = true,
            AutoHideTaskbar = false,
            KeepTaskbarOnTop = true,
            GroupSimilarButtons = true,
            ShowQuickLaunch = false,
            AgentAction = 0,
            SettingsVersion = 0,
            PendingApply = false
        };
}

public sealed class WindowsTaskbarCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsTaskbarCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsTaskbarCurrentData Data { get; set; } = new();
}

public sealed class WindowsTaskbarCurrentData
{
    public WindowsTaskbarTargetResponse Target { get; set; } = new();
    public WindowsTaskbarCurrentSettingsDto Settings { get; set; } = WindowsTaskbarCurrentSettingsDto.CreateFusionXDefaults();
    public WindowsTaskbarCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsTaskbarCurrentResult
{
    public WindowsTaskbarCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsTaskbarCurrentResult Success(WindowsTaskbarCurrentResponse response) =>
        new() { Response = response };

    public static WindowsTaskbarCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsTaskbarErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsTaskbarTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsTaskbarSettingsRequest
{
    public bool LockTaskbar { get; set; } = true;
    public bool AutoHideTaskbar { get; set; }
    public bool KeepTaskbarOnTop { get; set; } = true;
    public bool GroupSimilarButtons { get; set; } = true;
    public bool ShowQuickLaunch { get; set; }
}

public sealed class WindowsTaskbarExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}

public sealed class WindowsTaskbarOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsTaskbarExecuteNowRequest
{
    public WindowsTaskbarTargetRequest Target { get; set; } = new();
    public WindowsTaskbarSettingsRequest Settings { get; set; } = new();
    public WindowsTaskbarExecutionRequest Execution { get; set; } = new();
    public WindowsTaskbarOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsTaskbarQueueRequest
{
    public WindowsTaskbarTargetRequest Target { get; set; } = new();
    public WindowsTaskbarSettingsRequest Settings { get; set; } = new();
    public WindowsTaskbarExecutionRequest Execution { get; set; } = new();
    public WindowsTaskbarOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsTaskbarTemplateQueueRequest
{
    public WindowsTaskbarTargetRequest Target { get; set; } = new();
    public WindowsTaskbarSettingsRequest Settings { get; set; } = new();
    public WindowsTaskbarExecutionRequest Execution { get; set; } = new();
    public WindowsTaskbarOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsTaskbarExecuteNowBulkRequest
{
    public List<WindowsTaskbarTargetRequest> Targets { get; set; } = [];
    public WindowsTaskbarSettingsRequest Settings { get; set; } = new();
    public WindowsTaskbarExecutionRequest Execution { get; set; } = new();
    public WindowsTaskbarOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsTaskbarExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsTaskbarSettingsRequest Settings { get; set; } = new();
    public WindowsTaskbarExecutionRequest Execution { get; set; } = new();
    public WindowsTaskbarOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsTaskbarExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsTaskbarLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsTaskbarExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsTaskbarExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsTaskbarExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsTaskbarTargetResponse Target { get; set; } = new();
    public WindowsTaskbarExecutionResponse Execution { get; set; } = new();
    public WindowsTaskbarLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsTaskbarQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsTaskbarQueueData Data { get; set; } = new();
}

public sealed class WindowsTaskbarQueueData
{
    public Guid TaskId { get; set; }
    public WindowsTaskbarTargetResponse Target { get; set; } = new();
    public WindowsTaskbarExecutionResponse Execution { get; set; } = new();
    public WindowsTaskbarTemplateInfo? Template { get; set; }
    public WindowsTaskbarLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsTaskbarTemplateInfo
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}

public sealed class WindowsTaskbarExecuteNowResult
{
    public WindowsTaskbarExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsTaskbarExecuteNowResult Success(WindowsTaskbarExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsTaskbarExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsTaskbarQueueResult
{
    public WindowsTaskbarQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsTaskbarQueueResult Success(WindowsTaskbarQueueResponse response) =>
        new() { Response = response };

    public static WindowsTaskbarQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsTaskbarHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsTaskbarHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsTaskbarHistoryData Data { get; set; } = new();
}

public sealed class WindowsTaskbarHistoryData
{
    public WindowsTaskbarTargetResponse Target { get; set; } = new();
    public List<WindowsTaskbarHistoryItem> Items { get; set; } = [];
    public WindowsTaskbarPagination Pagination { get; set; } = new();
}

public sealed class WindowsTaskbarHistoryItem
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

public sealed class WindowsTaskbarPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsTaskbarHistoryResult
{
    public WindowsTaskbarHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsTaskbarHistoryResult Success(WindowsTaskbarHistoryResponse response) =>
        new() { Response = response };

    public static WindowsTaskbarHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsTaskbarBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsTaskbarBulkData Data { get; set; } = new();
}

public sealed class WindowsTaskbarBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsTaskbarTargetResult> Results { get; set; } = [];
    public WindowsTaskbarLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsTaskbarTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsTaskbarBulkResult
{
    public WindowsTaskbarBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsTaskbarBulkResult Success(WindowsTaskbarBulkResponse response) =>
        new() { Response = response };

    public static WindowsTaskbarBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsTaskbarLiveSettingsDto
{
    public bool LockTaskbar { get; set; } = true;
    public bool AutoHideTaskbar { get; set; }
    public bool KeepTaskbarOnTop { get; set; } = true;
    public bool GroupSimilarButtons { get; set; } = true;
    public bool ShowQuickLaunch { get; set; }
    public bool ShowClock { get; set; }
    public bool HideInactiveIcons { get; set; }
    public long ReportVersion { get; set; }
    public DateTime CollectedUtc { get; set; }
}

public sealed class WindowsTaskbarLiveResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsTaskbarLiveData Data { get; set; } = new();
}

public sealed class WindowsTaskbarLiveData
{
    public WindowsTaskbarTargetResponse Target { get; set; } = new();
    public WindowsTaskbarLiveSettingsDto? Settings { get; set; }
    public WindowsTaskbarCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsTaskbarLiveResult
{
    public WindowsTaskbarLiveResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsTaskbarLiveResult Success(WindowsTaskbarLiveResponse response) =>
        new() { Response = response };

    public static WindowsTaskbarLiveResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsTaskbarRefreshLiveOptionsRequest
{
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsTaskbarRefreshLiveResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsTaskbarRefreshLiveData Data { get; set; } = new();
}

public sealed class WindowsTaskbarRefreshLiveData
{
    public Guid TaskId { get; set; }
    public WindowsTaskbarTargetResponse Target { get; set; } = new();
    public WindowsTaskbarExecutionResponse Execution { get; set; } = new();
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsTaskbarRefreshLiveResult
{
    public WindowsTaskbarRefreshLiveResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsTaskbarRefreshLiveResult Success(WindowsTaskbarRefreshLiveResponse response) =>
        new() { Response = response };

    public static WindowsTaskbarRefreshLiveResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
