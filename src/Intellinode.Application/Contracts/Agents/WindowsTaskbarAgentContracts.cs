using System.Text.Json;

namespace Intellinode.Application.Contracts.Agents;

public sealed class AgentTaskbarLiveReportRequest
{
    /// <summary>Flat REST shape (preferred for Intellinode agents).</summary>
    public bool? LockTaskbar { get; set; }
    public bool? AutoHideTaskbar { get; set; }
    public bool? KeepTaskbarOnTop { get; set; }
    public bool? GroupSimilarButtons { get; set; }
    public bool? ShowQuickLaunch { get; set; }
    public bool? ShowClock { get; set; }
    public bool? HideInactiveIcons { get; set; }

    /// <summary>FusionX wrapper: <c>WinCELinux.XPTaskbarProperties</c>.</summary>
    public JsonElement? WinCELinux { get; set; }

    public int? LegacyTaskId { get; set; }
}

public sealed class AgentTaskbarLiveReportResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long ReportVersion { get; set; }
    public DateTime CollectedUtc { get; set; }
}

public sealed class AgentTaskbarLiveReportResult
{
    public AgentTaskbarLiveReportResponse? Response { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public bool IsSuccess => Response is not null;

    public static AgentTaskbarLiveReportResult Success(AgentTaskbarLiveReportResponse response) =>
        new() { Response = response };

    public static AgentTaskbarLiveReportResult Failure(string errorCode, string message) =>
        new() { ErrorCode = errorCode, Message = message };
}
