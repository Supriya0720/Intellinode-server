namespace Intellinode.Application.Contracts.Admin;

using Intellinode.Domain.Enums;

/// <summary>
/// FusionX Administration → Application command (ModuleType <c>Application</c> or <c>Command</c>).
/// Agent payloads: <c>WinCELinux.Application</c> / <c>WinCELinux.Command</c> (structXP_Data.cs ~L4871).
/// </summary>
public static class WindowsApplicationCommandModuleConstants
{
    public const string ApplicationModuleName = "Application";
    public const string CommandModuleName = "Command";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string TemplateQueueFunctionName = "QueueTemplate";
    /// <summary>FusionX bulk signal prefix (<c>{mac}&amp;196&amp;Insert&amp;</c>); single-device handler often leaves Signal empty.</summary>
    public const string DefaultSignalSuffix = "196";
    public const int MaxFunctionParameterLength = 512;

    /// <summary>PR2 validation caps chosen so worst-case inline JSON stays ≤512 (spike-tested).</summary>
    public const int MaxApplicationPathLength = 120;
    public const int MaxParametersLength = 32;
    public const int MaxAlertTitleLength = 32;
    public const int MaxAlertMessageLength = 87;
    public const int MaxMessageTypeLength = 4;
    public const int MaxDisplayTimeLength = 4;
    public const int MaxCommandTextLength = 200;
    public const int MaxTimeoutLength = 4;

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

    public static string ResolveModuleName(string mode) =>
        string.Equals(mode, ApplicationModuleName, StringComparison.OrdinalIgnoreCase)
            ? ApplicationModuleName
            : CommandModuleName;

    public static SettingsKind ResolveSettingsKind(string mode) =>
        string.Equals(mode, ApplicationModuleName, StringComparison.OrdinalIgnoreCase)
            ? SettingsKind.WindowsApplication
            : SettingsKind.WindowsCommand;

    public static WindowsApplicationCommandMode ParseMode(string? mode) =>
        string.Equals(mode, CommandModuleName, StringComparison.OrdinalIgnoreCase)
            ? WindowsApplicationCommandMode.Command
            : WindowsApplicationCommandMode.Application;

    public static string FormatMode(WindowsApplicationCommandMode mode) =>
        mode == WindowsApplicationCommandMode.Command ? CommandModuleName : ApplicationModuleName;
}

public enum WindowsApplicationCommandMode
{
    Application,
    Command
}

/// <summary>
/// Input for <see cref="Interfaces.IWindowsApplicationCommandPayloadBuilder.BuildAgentPayload"/>.
/// Field mapping follows FusionX <c>WinCELinux.Application</c> / <c>WinCELinux.Command</c>.
/// </summary>
public sealed class WindowsApplicationCommandPayloadRequest
{
    public WindowsApplicationCommandMode Mode { get; set; } = WindowsApplicationCommandMode.Application;
    public string ApplicationPath { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public bool WarnUser { get; set; }
    public string AlertTitle { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string DisplayTime { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public string Timeout { get; set; } = string.Empty;
    /// <summary>FusionX <c>Text1</c> — reboot required flag (<c>"0"</c>/<c>"1"</c>).</summary>
    public bool RebootRequired { get; set; }
    /// <summary>FusionX Command <c>Text2</c> — capture command output (<c>"0"</c>/<c>"1"</c>).</summary>
    public bool RequireCommandOutput { get; set; }
    public long TaskId { get; set; }
    public int AgentAction { get; set; }
}

public sealed class WindowsApplicationCommandTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsApplicationCommandCurrentSettingsDto
{
    public string Mode { get; set; } = WindowsApplicationCommandModuleConstants.ApplicationModuleName;
    public string ApplicationPath { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public bool WarnUser { get; set; }
    public string AlertTitle { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string DisplayTime { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public string Timeout { get; set; } = string.Empty;
    public bool RebootRequired { get; set; }
    public bool RequireCommandOutput { get; set; }
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }

    public static WindowsApplicationCommandCurrentSettingsDto CreateFusionXDefaults(
        WindowsApplicationCommandMode mode = WindowsApplicationCommandMode.Application) =>
        new()
        {
            Mode = WindowsApplicationCommandModuleConstants.FormatMode(mode),
            ApplicationPath = string.Empty,
            Parameters = string.Empty,
            WarnUser = false,
            AlertTitle = string.Empty,
            AlertMessage = string.Empty,
            MessageType = string.Empty,
            DisplayTime = string.Empty,
            CommandText = string.Empty,
            Timeout = string.Empty,
            RebootRequired = false,
            RequireCommandOutput = false,
            AgentAction = 0,
            SettingsVersion = 0,
            PendingApply = false
        };
}

public sealed class WindowsApplicationCommandCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsApplicationCommandCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsApplicationCommandCurrentData Data { get; set; } = new();
}

public sealed class WindowsApplicationCommandCurrentData
{
    public WindowsApplicationCommandTargetResponse Target { get; set; } = new();
    public WindowsApplicationCommandCurrentSettingsDto Settings { get; set; } =
        WindowsApplicationCommandCurrentSettingsDto.CreateFusionXDefaults();
    public WindowsApplicationCommandCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsApplicationCommandCurrentResult
{
    public WindowsApplicationCommandCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsApplicationCommandCurrentResult Success(WindowsApplicationCommandCurrentResponse response) =>
        new() { Response = response };

    public static WindowsApplicationCommandCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsApplicationCommandErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsApplicationCommandSettingsRequest
{
    public string Mode { get; set; } = WindowsApplicationCommandModuleConstants.ApplicationModuleName;
    public string ApplicationPath { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public bool WarnUser { get; set; }
    public string AlertTitle { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string DisplayTime { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public string Timeout { get; set; } = string.Empty;
    public bool RebootRequired { get; set; }
    public bool RequireCommandOutput { get; set; }
}

public sealed class WindowsApplicationCommandTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsApplicationCommandExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}

public sealed class WindowsApplicationCommandOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsApplicationCommandExecuteNowRequest
{
    public WindowsApplicationCommandTargetRequest Target { get; set; } = new();
    public WindowsApplicationCommandSettingsRequest Settings { get; set; } = new();
    public WindowsApplicationCommandExecutionRequest Execution { get; set; } = new();
    public WindowsApplicationCommandOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsApplicationCommandQueueRequest
{
    public WindowsApplicationCommandTargetRequest Target { get; set; } = new();
    public WindowsApplicationCommandSettingsRequest Settings { get; set; } = new();
    public WindowsApplicationCommandExecutionRequest Execution { get; set; } = new();
    public WindowsApplicationCommandOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsApplicationCommandTemplateQueueRequest
{
    public WindowsApplicationCommandTargetRequest Target { get; set; } = new();
    public WindowsApplicationCommandSettingsRequest Settings { get; set; } = new();
    public WindowsApplicationCommandExecutionRequest Execution { get; set; } = new();
    public WindowsApplicationCommandOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsApplicationCommandExecuteNowBulkRequest
{
    public List<WindowsApplicationCommandTargetRequest> Targets { get; set; } = [];
    public WindowsApplicationCommandSettingsRequest Settings { get; set; } = new();
    public WindowsApplicationCommandExecutionRequest Execution { get; set; } = new();
    public WindowsApplicationCommandOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsApplicationCommandExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsApplicationCommandSettingsRequest Settings { get; set; } = new();
    public WindowsApplicationCommandExecutionRequest Execution { get; set; } = new();
    public WindowsApplicationCommandOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsApplicationCommandExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsApplicationCommandLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsApplicationCommandExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsApplicationCommandExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsApplicationCommandExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsApplicationCommandTargetResponse Target { get; set; } = new();
    public WindowsApplicationCommandExecutionResponse Execution { get; set; } = new();
    public WindowsApplicationCommandLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsApplicationCommandQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsApplicationCommandQueueData Data { get; set; } = new();
}

public sealed class WindowsApplicationCommandQueueData
{
    public Guid TaskId { get; set; }
    public WindowsApplicationCommandTargetResponse Target { get; set; } = new();
    public WindowsApplicationCommandExecutionResponse Execution { get; set; } = new();
    public WindowsApplicationCommandTemplateInfo? Template { get; set; }
    public WindowsApplicationCommandLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsApplicationCommandTemplateInfo
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}

public sealed class WindowsApplicationCommandBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsApplicationCommandBulkData Data { get; set; } = new();
}

public sealed class WindowsApplicationCommandBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsApplicationCommandTargetResult> Results { get; set; } = [];
    public WindowsApplicationCommandLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsApplicationCommandTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsApplicationCommandBulkResult
{
    public WindowsApplicationCommandBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsApplicationCommandBulkResult Success(WindowsApplicationCommandBulkResponse response) =>
        new() { Response = response };

    public static WindowsApplicationCommandBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsApplicationCommandExecuteNowResult
{
    public WindowsApplicationCommandExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsApplicationCommandExecuteNowResult Success(WindowsApplicationCommandExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsApplicationCommandExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsApplicationCommandQueueResult
{
    public WindowsApplicationCommandQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsApplicationCommandQueueResult Success(WindowsApplicationCommandQueueResponse response) =>
        new() { Response = response };

    public static WindowsApplicationCommandQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsApplicationCommandHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsApplicationCommandHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsApplicationCommandHistoryData Data { get; set; } = new();
}

public sealed class WindowsApplicationCommandHistoryData
{
    public WindowsApplicationCommandTargetResponse Target { get; set; } = new();
    public List<WindowsApplicationCommandHistoryItem> Items { get; set; } = [];
    public WindowsApplicationCommandPagination Pagination { get; set; } = new();
}

public sealed class WindowsApplicationCommandHistoryItem
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

public sealed class WindowsApplicationCommandPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsApplicationCommandHistoryResult
{
    public WindowsApplicationCommandHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsApplicationCommandHistoryResult Success(WindowsApplicationCommandHistoryResponse response) =>
        new() { Response = response };

    public static WindowsApplicationCommandHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
