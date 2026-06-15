using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Builds FusionX-shaped taskbar agent payloads (<c>WinCELinux.XPTaskbarProperties</c>).
/// </summary>
public sealed class WindowsTaskbarPayloadBuilder : IWindowsTaskbarPayloadBuilder
{
    public string BuildAgentPayload(WindowsTaskbarPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskbar = new
        {
            blTaskbarLock = request.LockTaskbar,
            blAutoHideTaskbar = request.AutoHideTaskbar,
            blKeepTaskbarOnTop = request.KeepTaskbarOnTop,
            blGroupSimillarTaskbarButtons = request.GroupSimilarButtons,
            blShowQuckLaunch = request.ShowQuickLaunch,
            TaskID = request.TaskId,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPTaskbarProperties = taskbar } });
    }

    public WindowsTaskbarPayloadRequest MapToPayloadRequest(
        DeviceWindowsTaskbarSettings settings,
        long taskId,
        int agentAction)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new WindowsTaskbarPayloadRequest
        {
            LockTaskbar = settings.LockTaskbar,
            AutoHideTaskbar = settings.AutoHideTaskbar,
            KeepTaskbarOnTop = settings.KeepTaskbarOnTop,
            GroupSimilarButtons = settings.GroupSimilarButtons,
            ShowQuickLaunch = settings.ShowQuickLaunch,
            TaskId = taskId,
            AgentAction = agentAction
        };
    }

    public string BuildExtraData(string macAddress, string? signalSuffix = null)
    {
        var normalizedMac = macAddress.Trim();
        if (string.IsNullOrWhiteSpace(normalizedMac))
        {
            throw new ArgumentException("macAddress is required.", nameof(macAddress));
        }

        var suffix = string.IsNullOrWhiteSpace(signalSuffix)
            ? WindowsTaskbarModuleConstants.DefaultSignalSuffix
            : signalSuffix.Trim();

        return $"{normalizedMac}&{suffix}";
    }
}
