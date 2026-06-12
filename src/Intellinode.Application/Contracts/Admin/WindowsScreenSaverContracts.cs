namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// FusionX User Settings → Screen Saver (ModuleType <c>ScreenSaver</c>, signal <c>SCR</c>).
/// </summary>
public static class WindowsScreenSaverModuleConstants
{
    public const string ModuleName = "ScreenSaver";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string TemplateQueueFunctionName = "QueueTemplate";
    public const string DefaultSignalSuffix = "SCR";

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
    public const int MaxFunctionParameterLength = 512;
    public const int MaxScreenSaverNameLength = 128;
    public const int MaxSourceTypeLength = 32;
    public const int MaxRepositoryFieldLength = 256;
    public const int MaxFtpFolderPathLength = 512;
    public const int MaxFtpPasswordLength = 128;

    public static readonly string[] AllowedSourceTypes = ["Browse", "Upload", "Repository"];
}

/// <summary>
/// Input for <see cref="Interfaces.IWindowsScreenSaverPayloadBuilder.BuildAgentPayload"/>.
/// Field mapping follows FusionX <c>WinCELinux.XPScreenSaver</c> (structXP_Data.cs).
/// </summary>
public sealed class WindowsScreenSaverPayloadRequest
{
    public int TimeoutMinutes { get; set; }
    public bool PasswordProtected { get; set; }
    public string ScreenSaverName { get; set; } = string.Empty;
    public bool PreventUserChanges { get; set; }
    public bool Upload { get; set; }
    /// <summary>Browse, Upload, or Repository (FusionX <c>RepositoryType</c> / UI source).</summary>
    public string SourceType { get; set; } = "Browse";
    public int ConnectionId { get; set; }
    public string DownloadIp { get; set; } = string.Empty;
    public string FtpFolderPath { get; set; } = string.Empty;
    public string FtpPassword { get; set; } = string.Empty;
    public string FtpSslType { get; set; } = string.Empty;
    public string FtpUsername { get; set; } = string.Empty;
    public int LoggedInUserId { get; set; }
    public int Port { get; set; }
    public string ProtocolType { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public string DomainNameForRepository { get; set; } = string.Empty;
    public long TaskId { get; set; }
    public int AgentAction { get; set; }
}

public sealed class WindowsScreenSaverTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsScreenSaverCurrentSettingsDto
{
    public string ScreenSaverName { get; set; } = string.Empty;
    public int TimeoutMinutes { get; set; }
    public bool PasswordProtected { get; set; }
    public bool PreventUserChanges { get; set; }
    public string SourceType { get; set; } = "Browse";
    public bool Upload { get; set; }
    public int AgentAction { get; set; }
    public bool HasRepositoryMetadata { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
}

public sealed class WindowsScreenSaverCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsScreenSaverCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsScreenSaverCurrentData Data { get; set; } = new();
}

public sealed class WindowsScreenSaverCurrentData
{
    public WindowsScreenSaverTargetResponse Target { get; set; } = new();
    public WindowsScreenSaverCurrentSettingsDto Settings { get; set; } = new();
    public WindowsScreenSaverCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsScreenSaverCurrentResult
{
    public WindowsScreenSaverCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsScreenSaverCurrentResult Success(WindowsScreenSaverCurrentResponse response) =>
        new() { Response = response };

    public static WindowsScreenSaverCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsScreenSaverCatalogItemDto
{
    public string ScreenSaverName { get; set; } = string.Empty;
}

public sealed class WindowsScreenSaverCatalogCompatDto
{
    /// <summary>stub | device | none</summary>
    public string Source { get; set; } = "stub";
}

public sealed class WindowsScreenSaverCatalogResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsScreenSaverCatalogData Data { get; set; } = new();
}

public sealed class WindowsScreenSaverCatalogData
{
    public WindowsScreenSaverTargetResponse Target { get; set; } = new();
    public IReadOnlyList<WindowsScreenSaverCatalogItemDto> Items { get; set; } = [];
    public WindowsScreenSaverCatalogCompatDto Compat { get; set; } = new();
}

public sealed class WindowsScreenSaverCatalogResult
{
    public WindowsScreenSaverCatalogResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsScreenSaverCatalogResult Success(WindowsScreenSaverCatalogResponse response) =>
        new() { Response = response };

    public static WindowsScreenSaverCatalogResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsScreenSaverErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsScreenSaverTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsScreenSaverRepositoryRequest
{
    public int ConnectionId { get; set; }
    public string DownloadIp { get; set; } = string.Empty;
    public string FtpFolderPath { get; set; } = string.Empty;
    public string FtpPassword { get; set; } = string.Empty;
    public string FtpSslType { get; set; } = string.Empty;
    public string FtpUsername { get; set; } = string.Empty;
    public int LoggedInUserId { get; set; }
    public int Port { get; set; }
    public string ProtocolType { get; set; } = string.Empty;
    public string ConnectionName { get; set; } = string.Empty;
    public string DomainNameForRepository { get; set; } = string.Empty;
}

public sealed class WindowsScreenSaverSettingsRequest
{
    public string ScreenSaverName { get; set; } = string.Empty;
    public int TimeoutMinutes { get; set; }
    public bool PasswordProtected { get; set; }
    public bool PreventUserChanges { get; set; }
    /// <summary>Browse, Upload, or Repository (FusionX <c>RepositoryType</c>).</summary>
    public string SourceType { get; set; } = "Browse";
    public bool Upload { get; set; }
    public WindowsScreenSaverRepositoryRequest? Repository { get; set; }
}

public sealed class WindowsScreenSaverExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}

public sealed class WindowsScreenSaverOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsScreenSaverExecuteNowRequest
{
    public WindowsScreenSaverTargetRequest Target { get; set; } = new();
    public WindowsScreenSaverSettingsRequest Settings { get; set; } = new();
    public WindowsScreenSaverExecutionRequest Execution { get; set; } = new();
    public WindowsScreenSaverOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsScreenSaverQueueRequest
{
    public WindowsScreenSaverTargetRequest Target { get; set; } = new();
    public WindowsScreenSaverSettingsRequest Settings { get; set; } = new();
    public WindowsScreenSaverExecutionRequest Execution { get; set; } = new();
    public WindowsScreenSaverOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsScreenSaverTemplateQueueRequest
{
    public WindowsScreenSaverTargetRequest Target { get; set; } = new();
    public WindowsScreenSaverSettingsRequest Settings { get; set; } = new();
    public WindowsScreenSaverExecutionRequest Execution { get; set; } = new();
    public WindowsScreenSaverOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsScreenSaverExecuteNowBulkRequest
{
    public List<WindowsScreenSaverTargetRequest> Targets { get; set; } = [];
    public WindowsScreenSaverSettingsRequest Settings { get; set; } = new();
    public WindowsScreenSaverExecutionRequest Execution { get; set; } = new();
    public WindowsScreenSaverOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsScreenSaverExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsScreenSaverSettingsRequest Settings { get; set; } = new();
    public WindowsScreenSaverExecutionRequest Execution { get; set; } = new();
    public WindowsScreenSaverOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsScreenSaverExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsScreenSaverLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsScreenSaverExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsScreenSaverExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsScreenSaverExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsScreenSaverTargetResponse Target { get; set; } = new();
    public WindowsScreenSaverExecutionResponse Execution { get; set; } = new();
    public WindowsScreenSaverLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsScreenSaverQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsScreenSaverQueueData Data { get; set; } = new();
}

public sealed class WindowsScreenSaverQueueData
{
    public Guid TaskId { get; set; }
    public WindowsScreenSaverTargetResponse Target { get; set; } = new();
    public WindowsScreenSaverExecutionResponse Execution { get; set; } = new();
    public WindowsScreenSaverTemplateInfo? Template { get; set; }
    public WindowsScreenSaverLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsScreenSaverTemplateInfo
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}

public sealed class WindowsScreenSaverBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsScreenSaverBulkData Data { get; set; } = new();
}

public sealed class WindowsScreenSaverBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsScreenSaverTargetResult> Results { get; set; } = [];
    public WindowsScreenSaverLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsScreenSaverTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsScreenSaverBulkResult
{
    public WindowsScreenSaverBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsScreenSaverBulkResult Success(WindowsScreenSaverBulkResponse response) =>
        new() { Response = response };

    public static WindowsScreenSaverBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsScreenSaverExecuteNowResult
{
    public WindowsScreenSaverExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsScreenSaverExecuteNowResult Success(WindowsScreenSaverExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsScreenSaverExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsScreenSaverQueueResult
{
    public WindowsScreenSaverQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsScreenSaverQueueResult Success(WindowsScreenSaverQueueResponse response) =>
        new() { Response = response };

    public static WindowsScreenSaverQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsScreenSaverHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsScreenSaverHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsScreenSaverHistoryData Data { get; set; } = new();
}

public sealed class WindowsScreenSaverHistoryData
{
    public WindowsScreenSaverTargetResponse Target { get; set; } = new();
    public List<WindowsScreenSaverHistoryItem> Items { get; set; } = [];
    public WindowsScreenSaverPagination Pagination { get; set; } = new();
}

public sealed class WindowsScreenSaverHistoryItem
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

public sealed class WindowsScreenSaverPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsScreenSaverHistoryResult
{
    public WindowsScreenSaverHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsScreenSaverHistoryResult Success(WindowsScreenSaverHistoryResponse response) =>
        new() { Response = response };

    public static WindowsScreenSaverHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
