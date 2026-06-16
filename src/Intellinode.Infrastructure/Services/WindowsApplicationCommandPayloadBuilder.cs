using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Application.Validation;
using Intellinode.Domain.Entities;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Builds FusionX-shaped application/command agent payloads (<c>WinCELinux.Application</c> / <c>WinCELinux.Command</c>).
/// PR0 spike / PR1+ apply pipeline.
/// </summary>
public sealed class WindowsApplicationCommandPayloadBuilder : IWindowsApplicationCommandPayloadBuilder
{
    public string BuildAgentPayload(WindowsApplicationCommandPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return WindowsApplicationCommandRequestValidation.SerializePayload(request);
    }

    public WindowsApplicationCommandPayloadRequest MapApplicationToPayloadRequest(
        string applicationPath,
        string parameters,
        bool warnUser,
        string alertTitle,
        string alertMessage,
        string messageType,
        string displayTime,
        bool rebootRequired,
        long taskId,
        int agentAction)
    {
        return new WindowsApplicationCommandPayloadRequest
        {
            Mode = WindowsApplicationCommandMode.Application,
            ApplicationPath = applicationPath,
            Parameters = parameters,
            WarnUser = warnUser,
            AlertTitle = alertTitle,
            AlertMessage = alertMessage,
            MessageType = messageType,
            DisplayTime = displayTime,
            RebootRequired = rebootRequired,
            TaskId = taskId,
            AgentAction = agentAction
        };
    }

    public WindowsApplicationCommandPayloadRequest MapCommandToPayloadRequest(
        string commandText,
        string timeout,
        bool rebootRequired,
        bool requireCommandOutput,
        long taskId,
        int agentAction)
    {
        return new WindowsApplicationCommandPayloadRequest
        {
            Mode = WindowsApplicationCommandMode.Command,
            CommandText = commandText,
            Timeout = timeout,
            RebootRequired = rebootRequired,
            RequireCommandOutput = requireCommandOutput,
            TaskId = taskId,
            AgentAction = agentAction
        };
    }

    public WindowsApplicationCommandPayloadRequest MapToPayloadRequest(
        DeviceWindowsApplicationCommandSettings settings,
        long taskId,
        int agentAction)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var mode = WindowsApplicationCommandModuleConstants.ParseMode(settings.Mode);
        if (mode == WindowsApplicationCommandMode.Command)
        {
            return MapCommandToPayloadRequest(
                settings.CommandText,
                settings.Timeout,
                settings.RebootRequired,
                settings.RequireCommandOutput,
                taskId,
                agentAction);
        }

        return MapApplicationToPayloadRequest(
            settings.ApplicationPath,
            settings.Parameters,
            settings.WarnUser,
            settings.AlertTitle,
            settings.AlertMessage,
            settings.MessageType,
            settings.DisplayTime,
            settings.RebootRequired,
            taskId,
            agentAction);
    }

    public string BuildExtraData(string macAddress, string? signalSuffix = null)
    {
        var normalizedMac = macAddress.Trim();
        if (string.IsNullOrWhiteSpace(normalizedMac))
        {
            throw new ArgumentException("macAddress is required.", nameof(macAddress));
        }

        var suffix = string.IsNullOrWhiteSpace(signalSuffix)
            ? WindowsApplicationCommandModuleConstants.DefaultSignalSuffix
            : signalSuffix.Trim();

        return $"{normalizedMac}&{suffix}";
    }
}
