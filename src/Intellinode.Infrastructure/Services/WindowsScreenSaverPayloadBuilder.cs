using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Builds FusionX-shaped screen saver agent payloads (<c>WinCELinux.XPScreenSaver</c>).
/// PR0 spike / PR1+ apply pipeline.
/// </summary>
public sealed class WindowsScreenSaverPayloadBuilder : IWindowsScreenSaverPayloadBuilder
{
    public const int MaxCompactTaskReferenceLength = 72;

    public string BuildAgentPayload(WindowsScreenSaverPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var screenSaver = new
        {
            intScreenSaverTimeOut = request.TimeoutMinutes,
            blScreenSaverPasswordProtected = request.PasswordProtected,
            strCurrentScreenSaver = request.ScreenSaverName,
            blUpload = request.Upload,
            ConnectionId = request.ConnectionId,
            DownloadIP = request.DownloadIp,
            FTPFolderPath = request.FtpFolderPath,
            FTPpassword = request.FtpPassword,
            FTPSSLType = request.FtpSslType,
            FTPUsername = request.FtpUsername,
            LoggedInUserID = request.LoggedInUserId,
            Port = request.Port,
            ProtocolType = request.ProtocolType,
            RepositoryType = request.SourceType,
            strText1 = request.PreventUserChanges ? "true" : "false",
            strText2 = request.DomainNameForRepository,
            strText3 = string.Empty,
            strText4 = string.Empty,
            strText5 = string.Empty,
            TaskID = request.TaskId,
            AgentAction = request.AgentAction,
            ConnectionName = request.ConnectionName
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPScreenSaver = screenSaver } });
    }

    public WindowsScreenSaverPayloadRequest MapToPayloadRequest(
        DeviceWindowsScreenSaverSettings settings,
        long taskId,
        int agentAction)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var request = new WindowsScreenSaverPayloadRequest
        {
            TimeoutMinutes = settings.TimeoutMinutes,
            PasswordProtected = settings.PasswordProtected,
            ScreenSaverName = settings.ScreenSaverName,
            PreventUserChanges = settings.PreventUserChanges,
            Upload = settings.Upload,
            SourceType = settings.SourceType,
            TaskId = taskId,
            AgentAction = agentAction
        };

        if (!string.IsNullOrWhiteSpace(settings.RepositoryJson))
        {
            MergeRepositoryJson(settings.RepositoryJson, request);
        }

        return request;
    }

    public WindowsScreenSaverPayloadRequest MapToPayloadRequest(
        DeviceWindowsScreenSaverSettingsSnapshot snapshot,
        long taskId,
        int agentAction)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var request = new WindowsScreenSaverPayloadRequest
        {
            TimeoutMinutes = snapshot.TimeoutMinutes,
            PasswordProtected = snapshot.PasswordProtected,
            ScreenSaverName = snapshot.ScreenSaverName,
            PreventUserChanges = snapshot.PreventUserChanges,
            Upload = snapshot.Upload,
            SourceType = snapshot.SourceType,
            TaskId = taskId,
            AgentAction = agentAction
        };

        if (!string.IsNullOrWhiteSpace(snapshot.RepositoryJson))
        {
            MergeRepositoryJson(snapshot.RepositoryJson, request);
        }

        return request;
    }

    public string BuildCompactTaskReference(long settingsVersion)
    {
        if (settingsVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settingsVersion), "Settings version must be non-negative.");
        }

        var json = JsonSerializer.Serialize(new { settingsVersion });
        if (json.Length > WindowsScreenSaverModuleConstants.MaxFunctionParameterLength)
        {
            throw new InvalidOperationException(
                $"Compact task reference exceeds {WindowsScreenSaverModuleConstants.MaxFunctionParameterLength} characters ({json.Length}).");
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

        var suffix = string.IsNullOrWhiteSpace(signalSuffix)
            ? WindowsScreenSaverModuleConstants.DefaultSignalSuffix
            : signalSuffix.Trim();

        return $"{normalizedMac}&{suffix}";
    }

    private static void MergeRepositoryJson(string repositoryJson, WindowsScreenSaverPayloadRequest request)
    {
        try
        {
            using var document = JsonDocument.Parse(repositoryJson);
            var root = document.RootElement;

            request.ConnectionId = ReadInt(root, "connectionId", request.ConnectionId);
            request.DownloadIp = ReadString(root, "downloadIp", request.DownloadIp);
            request.FtpFolderPath = ReadString(root, "ftpFolderPath", request.FtpFolderPath);
            request.FtpPassword = ReadString(root, "ftpPassword", request.FtpPassword);
            request.FtpSslType = ReadString(root, "ftpSslType", request.FtpSslType);
            request.FtpUsername = ReadString(root, "ftpUsername", request.FtpUsername);
            request.LoggedInUserId = ReadInt(root, "loggedInUserId", request.LoggedInUserId);
            request.Port = ReadInt(root, "port", request.Port);
            request.ProtocolType = ReadString(root, "protocolType", request.ProtocolType);
            request.ConnectionName = ReadString(root, "connectionName", request.ConnectionName);
            request.DomainNameForRepository = ReadString(root, "domainNameForRepository", request.DomainNameForRepository);
        }
        catch (JsonException)
        {
            // PR3 will validate repository JSON at queue time.
        }
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? fallback
            : fallback;

    private static int ReadInt(JsonElement root, string propertyName, int fallback) =>
        root.TryGetProperty(propertyName, out var element) && element.TryGetInt32(out var value)
            ? value
            : fallback;
}
