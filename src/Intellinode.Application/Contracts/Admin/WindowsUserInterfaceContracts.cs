namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// FusionX User Settings → User Interface / Autologon (ModuleType <c>Autologon</c>).
/// Agent payload: <c>WinCELinux.XPAutologon</c>.
/// </summary>
public static class WindowsUserInterfaceModuleConstants
{
    public const string ModuleName = "Autologon";
    public const string InstantFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const string TemplateQueueFunctionName = "QueueTemplate";
    public const string DefaultSignalSuffix = "";
    public const int MaxFunctionParameterLength = 512;
    public const int MaxUserNameLength = 256;
    public const int MaxPasswordLength = 256;

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

/// <summary>
/// Input for <see cref="Interfaces.IWindowsUserInterfacePayloadBuilder.BuildAgentPayload"/>.
/// Field mapping follows FusionX <c>WinCELinux.XPAutologon</c> (structXP_Data.cs).
/// </summary>
public sealed class WindowsUserInterfacePayloadRequest
{
    public string UserName { get; set; } = string.Empty;
    public bool AutoLogon { get; set; }
    public string Password { get; set; } = string.Empty;
    public long TaskId { get; set; }
    public int AgentAction { get; set; }
}

public sealed class WindowsUserInterfaceTargetResponse
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsUserInterfaceCurrentSettingsDto
{
    public string UserName { get; set; } = string.Empty;
    public bool AutoLogon { get; set; }
    public bool HasPassword { get; set; }
    public int AgentAction { get; set; }
    public long SettingsVersion { get; set; }
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }

    public static WindowsUserInterfaceCurrentSettingsDto CreateFusionXDefaults() =>
        new()
        {
            UserName = string.Empty,
            AutoLogon = false,
            HasPassword = false,
            AgentAction = 0,
            SettingsVersion = 0,
            PendingApply = false
        };
}

public sealed class WindowsUserInterfaceCurrentCompatDto
{
    public string Source { get; set; } = "none";
}

public sealed class WindowsUserInterfaceCurrentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsUserInterfaceCurrentData Data { get; set; } = new();
}

public sealed class WindowsUserInterfaceCurrentData
{
    public WindowsUserInterfaceTargetResponse Target { get; set; } = new();
    public WindowsUserInterfaceCurrentSettingsDto Settings { get; set; } =
        WindowsUserInterfaceCurrentSettingsDto.CreateFusionXDefaults();
    public WindowsUserInterfaceCurrentCompatDto Compat { get; set; } = new();
}

public sealed class WindowsUserInterfaceCurrentResult
{
    public WindowsUserInterfaceCurrentResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsUserInterfaceCurrentResult Success(WindowsUserInterfaceCurrentResponse response) =>
        new() { Response = response };

    public static WindowsUserInterfaceCurrentResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsUserInterfaceUserItemDto
{
    public string UserName { get; set; } = string.Empty;
}

public sealed class WindowsUserInterfaceUsersCompatDto
{
    /// <summary>stub | device | none</summary>
    public string Source { get; set; } = "stub";
}

public sealed class WindowsUserInterfaceUsersResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsUserInterfaceUsersData Data { get; set; } = new();
}

public sealed class WindowsUserInterfaceUsersData
{
    public WindowsUserInterfaceTargetResponse Target { get; set; } = new();
    public IReadOnlyList<WindowsUserInterfaceUserItemDto> Items { get; set; } = [];
    public WindowsUserInterfaceUsersCompatDto Compat { get; set; } = new();
}

public sealed class WindowsUserInterfaceUsersResult
{
    public WindowsUserInterfaceUsersResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsUserInterfaceUsersResult Success(WindowsUserInterfaceUsersResponse response) =>
        new() { Response = response };

    public static WindowsUserInterfaceUsersResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsUserInterfaceErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsUserInterfaceTargetRequest
{
    public string MacAddress { get; set; } = string.Empty;
    public string OsType { get; set; } = string.Empty;
}

public sealed class WindowsUserInterfaceSettingsRequest
{
    public string UserName { get; set; } = string.Empty;
    public bool AutoLogon { get; set; }
    /// <summary>Plaintext password for agent apply. Omit when <see cref="KeepExistingPassword"/> is true.</summary>
    public string? Password { get; set; }
    /// <summary>Retain stored credential when password is omitted (device must already have one if autoLogon is true).</summary>
    public bool KeepExistingPassword { get; set; }
}

public sealed class WindowsUserInterfaceExecutionRequest
{
    public string AgentAction { get; set; } = "0";
    public string ScheduleType { get; set; } = "InstantApply";
    public int? TemplateId { get; set; }
    public string? TemplateName { get; set; }
}

public sealed class WindowsUserInterfaceOptionsRequest
{
    public bool DryRun { get; set; }
    public bool ReturnLegacySummary { get; set; } = true;
    public Guid? CorrelationId { get; set; }
}

public sealed class WindowsUserInterfaceExecuteNowRequest
{
    public WindowsUserInterfaceTargetRequest Target { get; set; } = new();
    public WindowsUserInterfaceSettingsRequest Settings { get; set; } = new();
    public WindowsUserInterfaceExecutionRequest Execution { get; set; } = new();
    public WindowsUserInterfaceOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsUserInterfaceQueueRequest
{
    public WindowsUserInterfaceTargetRequest Target { get; set; } = new();
    public WindowsUserInterfaceSettingsRequest Settings { get; set; } = new();
    public WindowsUserInterfaceExecutionRequest Execution { get; set; } = new();
    public WindowsUserInterfaceOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsUserInterfaceTemplateQueueRequest
{
    public WindowsUserInterfaceTargetRequest Target { get; set; } = new();
    public WindowsUserInterfaceSettingsRequest Settings { get; set; } = new();
    public WindowsUserInterfaceExecutionRequest Execution { get; set; } = new();
    public WindowsUserInterfaceOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsUserInterfaceExecuteNowBulkRequest
{
    public List<WindowsUserInterfaceTargetRequest> Targets { get; set; } = [];
    public WindowsUserInterfaceSettingsRequest Settings { get; set; } = new();
    public WindowsUserInterfaceExecutionRequest Execution { get; set; } = new();
    public WindowsUserInterfaceOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsUserInterfaceExecuteNowGroupRequest
{
    public Guid GroupId { get; set; }
    public WindowsUserInterfaceSettingsRequest Settings { get; set; } = new();
    public WindowsUserInterfaceExecutionRequest Execution { get; set; } = new();
    public WindowsUserInterfaceOptionsRequest Options { get; set; } = new();
}

public sealed class WindowsUserInterfaceExecutionResponse
{
    public string ScheduleType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime QueuedAtUtc { get; set; }
}

public sealed class WindowsUserInterfaceLegacySummary
{
    public string ErrorMsg { get; set; } = "...$ApplyGreenSuccess";
    public string QualifiedMsg { get; set; } = "1";
    public List<object> DtApproved { get; set; } = [];
    public string HtmlData { get; set; } = string.Empty;
}

public sealed class WindowsUserInterfaceExecuteNowResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsUserInterfaceExecuteNowData Data { get; set; } = new();
}

public sealed class WindowsUserInterfaceExecuteNowData
{
    public Guid TaskId { get; set; }
    public WindowsUserInterfaceTargetResponse Target { get; set; } = new();
    public WindowsUserInterfaceExecutionResponse Execution { get; set; } = new();
    public WindowsUserInterfaceLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsUserInterfaceQueueResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsUserInterfaceQueueData Data { get; set; } = new();
}

public sealed class WindowsUserInterfaceQueueData
{
    public Guid TaskId { get; set; }
    public WindowsUserInterfaceTargetResponse Target { get; set; } = new();
    public WindowsUserInterfaceExecutionResponse Execution { get; set; } = new();
    public WindowsUserInterfaceTemplateInfo? Template { get; set; }
    public WindowsUserInterfaceLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsUserInterfaceTemplateInfo
{
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
}

public sealed class WindowsUserInterfaceExecuteNowResult
{
    public WindowsUserInterfaceExecuteNowResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsUserInterfaceExecuteNowResult Success(WindowsUserInterfaceExecuteNowResponse response) =>
        new() { Response = response };

    public static WindowsUserInterfaceExecuteNowResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsUserInterfaceQueueResult
{
    public WindowsUserInterfaceQueueResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsUserInterfaceQueueResult Success(WindowsUserInterfaceQueueResponse response) =>
        new() { Response = response };

    public static WindowsUserInterfaceQueueResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsUserInterfaceHistoryQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class WindowsUserInterfaceHistoryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsUserInterfaceHistoryData Data { get; set; } = new();
}

public sealed class WindowsUserInterfaceHistoryData
{
    public WindowsUserInterfaceTargetResponse Target { get; set; } = new();
    public List<WindowsUserInterfaceHistoryItem> Items { get; set; } = [];
    public WindowsUserInterfacePagination Pagination { get; set; } = new();
}

public sealed class WindowsUserInterfaceHistoryItem
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

public sealed class WindowsUserInterfacePagination
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public sealed class WindowsUserInterfaceHistoryResult
{
    public WindowsUserInterfaceHistoryResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsUserInterfaceHistoryResult Success(WindowsUserInterfaceHistoryResponse response) =>
        new() { Response = response };

    public static WindowsUserInterfaceHistoryResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}

public sealed class WindowsUserInterfaceBulkResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public WindowsUserInterfaceBulkData Data { get; set; } = new();
}

public sealed class WindowsUserInterfaceBulkData
{
    public Guid TaskId { get; set; }
    public int TotalTargets { get; set; }
    public int Accepted { get; set; }
    public int Blocked { get; set; }
    public List<WindowsUserInterfaceTargetResult> Results { get; set; } = [];
    public WindowsUserInterfaceLegacySummary? LegacySummary { get; set; }
    public Guid CorrelationId { get; set; }
}

public sealed class WindowsUserInterfaceTargetResult
{
    public string MacAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public sealed class WindowsUserInterfaceBulkResult
{
    public WindowsUserInterfaceBulkResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static WindowsUserInterfaceBulkResult Success(WindowsUserInterfaceBulkResponse response) =>
        new() { Response = response };

    public static WindowsUserInterfaceBulkResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
