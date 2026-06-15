namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// FusionX User Settings → Wallpaper (ModuleType <c>Wallpaper</c>, signal <c>WPS</c>).
/// </summary>
public static class WindowsWallpaperModuleConstants
{
    public const string ModuleName = "Wallpaper";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string TemplateQueueFunctionName = "QueueTemplate";
    public const string DefaultSignalSuffix = "WPS";

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
    public const int MaxPicturePathLength = 512;
    public const int MaxPictureNameLength = 256;
    public const int MaxSourceTypeLength = 32;
    public const int MaxPicturePositionLength = 32;
    public const int MaxRepositoryFieldLength = 256;
    public const int MaxFtpFolderPathLength = 512;
    public const int MaxFtpPasswordLength = 128;

    public static readonly string[] AllowedSourceTypes = ["Browse", "Upload", "Repository"];
    public static readonly string[] AllowedPicturePositions = ["Stretch", "Tile", "Center"];
}

/// <summary>
/// Input for <see cref="Interfaces.IWindowsWallpaperPayloadBuilder.BuildAgentPayload"/>.
/// Field mapping follows FusionX <c>WinCELinux.XPWallPaper</c> (structXP_Data.cs).
/// </summary>
public sealed class WindowsWallpaperPayloadRequest
{
    public string PictureName { get; set; } = string.Empty;
    public string PicturePosition { get; set; } = string.Empty;
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

public sealed class WindowsWallpaperTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsWallpaperCurrentSettingsDto
{
    public string SourceType { get; set; } = "Browse";
    public string PicturePath { get; set; } = string.Empty;
    public string PictureName { get; set; } = string.Empty;
    public string PicturePosition { get; set; } = string.Empty;
    public bool PreventUserChanges { get; set; }
    public bool Upload { get; set; }
    public int AgentAction { get; set; }
    public bool HasRepositoryMetadata { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }

    public static WindowsWallpaperCurrentSettingsDto CreateFusionXDefaults() =>
        new()
        {
            SourceType = "Browse",
            PicturePath = string.Empty,
            PictureName = string.Empty,
            PicturePosition = string.Empty,
            PreventUserChanges = false,
            Upload = false,
            AgentAction = 0,
            SettingsVersion = 0,
            PendingApply = false
        };
}

public sealed class WindowsWallpaperCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsWallpaperCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWallpaperCurrentData Data { get; set; } = new();
}

public sealed class WindowsWallpaperCurrentData
{
    public WindowsWallpaperTargetResponse Target { get; set; } = new();
    public WindowsWallpaperCurrentSettingsDto Settings { get; set; } = WindowsWallpaperCurrentSettingsDto.CreateFusionXDefaults();
    public WindowsWallpaperCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsWallpaperCurrentResult
{
    public WindowsWallpaperCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWallpaperCurrentResult Success(WindowsWallpaperCurrentResponse response) =>
        new() { Response = response };

    public static WindowsWallpaperCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWallpaperErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWallpaperRepositoryRequest
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

public sealed class WindowsWallpaperSettingsRequest
{
    public string SourceType { get; set; } = "Browse";
    public string PicturePath { get; set; } = string.Empty;
    public string PictureName { get; set; } = string.Empty;
    public string PicturePosition { get; set; } = string.Empty;
    public bool PreventUserChanges { get; set; }
    public bool Upload { get; set; }
    public WindowsWallpaperRepositoryRequest? Repository { get; set; }
}

public sealed class WindowsWallpaperTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsWallpaperExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}

public sealed class WindowsWallpaperOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsWallpaperExecuteNowRequest
{
    public WindowsWallpaperTargetRequest Target { get; set; } = new();
    public WindowsWallpaperSettingsRequest Settings { get; set; } = new();
    public WindowsWallpaperExecutionRequest Execution { get; set; } = new();
    public WindowsWallpaperOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWallpaperQueueRequest
{
    public WindowsWallpaperTargetRequest Target { get; set; } = new();
    public WindowsWallpaperSettingsRequest Settings { get; set; } = new();
    public WindowsWallpaperExecutionRequest Execution { get; set; } = new();
    public WindowsWallpaperOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWallpaperTemplateQueueRequest
{
    public WindowsWallpaperTargetRequest Target { get; set; } = new();
    public WindowsWallpaperSettingsRequest Settings { get; set; } = new();
    public WindowsWallpaperExecutionRequest Execution { get; set; } = new();
    public WindowsWallpaperOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWallpaperExecuteNowBulkRequest
{
    public List<WindowsWallpaperTargetRequest> Targets { get; set; } = [];
    public WindowsWallpaperSettingsRequest Settings { get; set; } = new();
    public WindowsWallpaperExecutionRequest Execution { get; set; } = new();
    public WindowsWallpaperOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWallpaperExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsWallpaperSettingsRequest Settings { get; set; } = new();
    public WindowsWallpaperExecutionRequest Execution { get; set; } = new();
    public WindowsWallpaperOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsWallpaperExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsWallpaperLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsWallpaperExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWallpaperExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsWallpaperExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsWallpaperTargetResponse Target { get; set; } = new();
    public WindowsWallpaperExecutionResponse Execution { get; set; } = new();
    public WindowsWallpaperLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWallpaperExecuteNowResult
{
    public WindowsWallpaperExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWallpaperExecuteNowResult Success(WindowsWallpaperExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsWallpaperExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWallpaperQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWallpaperQueueData Data { get; set; } = new();
}

public sealed class WindowsWallpaperQueueData
{
    public Guid TaskId { get; set; }
    public WindowsWallpaperTargetResponse Target { get; set; } = new();
    public WindowsWallpaperExecutionResponse Execution { get; set; } = new();
    public WindowsWallpaperTemplateInfo? Template { get; set; }
    public WindowsWallpaperLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWallpaperTemplateInfo
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}

public sealed class WindowsWallpaperBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWallpaperBulkData Data { get; set; } = new();
}

public sealed class WindowsWallpaperBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsWallpaperTargetResult> Results { get; set; } = [];
    public WindowsWallpaperLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsWallpaperTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsWallpaperBulkResult
{
    public WindowsWallpaperBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWallpaperBulkResult Success(WindowsWallpaperBulkResponse response) =>
        new() { Response = response };

    public static WindowsWallpaperBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWallpaperQueueResult
{
    public WindowsWallpaperQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWallpaperQueueResult Success(WindowsWallpaperQueueResponse response) =>
        new() { Response = response };

    public static WindowsWallpaperQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsWallpaperHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsWallpaperHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsWallpaperHistoryData Data { get; set; } = new();
}

public sealed class WindowsWallpaperHistoryData
{
    public WindowsWallpaperTargetResponse Target { get; set; } = new();
    public List<WindowsWallpaperHistoryItem> Items { get; set; } = [];
    public WindowsWallpaperPagination Pagination { get; set; } = new();
}

public sealed class WindowsWallpaperHistoryItem
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

public sealed class WindowsWallpaperPagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsWallpaperHistoryResult
{
    public WindowsWallpaperHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsWallpaperHistoryResult Success(WindowsWallpaperHistoryResponse response) =>
        new() { Response = response };

    public static WindowsWallpaperHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
