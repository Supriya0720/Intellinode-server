using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Builds FusionX-shaped autologon agent payloads (<c>WinCELinux.XPAutologon</c>).
/// </summary>
public sealed class WindowsUserInterfacePayloadBuilder : IWindowsUserInterfacePayloadBuilder
{
    public string BuildAgentPayload(WindowsUserInterfacePayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var autologon = new
        {
            Name = request.UserName,
            Autologon = request.AutoLogon,
            password = request.Password,
            TaskID = request.TaskId,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPAutologon = autologon } });
    }

    public WindowsUserInterfacePayloadRequest MapToPayloadRequest(
        DeviceWindowsUserInterfaceSettings settings,
        long taskId,
        int agentAction,
        string password)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new WindowsUserInterfacePayloadRequest
        {
            UserName = settings.UserName,
            AutoLogon = settings.AutoLogon,
            Password = password,
            TaskId = taskId,
            AgentAction = agentAction
        };
    }

    public WindowsUserInterfacePayloadRequest MapToPayloadRequest(
        DeviceWindowsUserInterfaceSettingsSnapshot snapshot,
        long taskId,
        int agentAction,
        string password)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new WindowsUserInterfacePayloadRequest
        {
            UserName = snapshot.UserName,
            AutoLogon = snapshot.AutoLogon,
            Password = password,
            TaskId = taskId,
            AgentAction = agentAction
        };
    }

    public string BuildCompactTaskReference(long settingsVersion)
    {
        if (settingsVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settingsVersion), "Settings version must be non-negative.");
        }

        var json = JsonSerializer.Serialize(new { settingsVersion });
        if (json.Length > WindowsUserInterfaceModuleConstants.MaxFunctionParameterLength)
        {
            throw new InvalidOperationException(
                $"Compact task reference exceeds {WindowsUserInterfaceModuleConstants.MaxFunctionParameterLength} characters ({json.Length}).");
        }

        return json;
    }

    public bool TryParseCompactTaskReference(string stored, out long settingsVersion)
    {
        settingsVersion = 0;

        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(stored);
            if (!document.RootElement.TryGetProperty("settingsVersion", out var versionElement))
            {
                return false;
            }

            settingsVersion = versionElement.GetInt64();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public string BuildExtraData(string macAddress, string? signalSuffix = null)
    {
        var normalizedMac = macAddress.Trim();
        if (string.IsNullOrWhiteSpace(normalizedMac))
        {
            throw new ArgumentException("macAddress is required.", nameof(macAddress));
        }

        var suffix = signalSuffix?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(suffix)
            ? normalizedMac
            : $"{normalizedMac}&{suffix}";
    }
}
