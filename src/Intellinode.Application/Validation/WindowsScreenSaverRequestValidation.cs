using System.Text.Json;
using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Validation;

/// <summary>
/// PR3 repository/upload validation shared by FluentValidation and the settings service.
/// </summary>
public static class WindowsScreenSaverRequestValidation
{
    public const int MaxFunctionParameterLength = WindowsScreenSaverModuleConstants.MaxFunctionParameterLength;

    public static bool IsRepositoryPath(WindowsScreenSaverSettingsRequest settings) =>
        settings.Upload
        || string.Equals(settings.SourceType?.Trim(), "Repository", StringComparison.OrdinalIgnoreCase)
        || string.Equals(settings.SourceType?.Trim(), "Upload", StringComparison.OrdinalIgnoreCase);

    public static bool PayloadWithinLimit(WindowsScreenSaverSettingsRequest settings, int agentAction)
    {
        if (IsRepositoryPath(settings))
        {
            return JsonSerializer.Serialize(new { settingsVersion = 1L }).Length <= MaxFunctionParameterLength;
        }

        return SerializeBrowsePayload(settings, agentAction).Length <= MaxFunctionParameterLength;
    }

    public static string? ValidateRepositorySettings(WindowsScreenSaverSettingsRequest settings)
    {
        if (!IsRepositoryPath(settings))
        {
            if (settings.Upload)
            {
                return "upload must be false for browse path.";
            }

            if (settings.Repository is not null)
            {
                return "repository must be omitted for browse path.";
            }

            var sourceType = settings.SourceType?.Trim();
            if (!string.IsNullOrWhiteSpace(sourceType)
                && !WindowsScreenSaverModuleConstants.AllowedSourceTypes.Contains(sourceType, StringComparer.OrdinalIgnoreCase))
            {
                return "sourceType must be one of Browse, Upload, Repository.";
            }

            return null;
        }

        if (settings.Repository is null)
        {
            return "repository is required for upload/repository path.";
        }

        return ValidateRepository(settings.Repository);
    }

    public static string? ValidateRepository(WindowsScreenSaverRepositoryRequest repository)
    {
        if (string.IsNullOrWhiteSpace(repository.DownloadIp))
        {
            return "repository.downloadIp is required.";
        }

        if (repository.DownloadIp.Length > WindowsScreenSaverModuleConstants.MaxRepositoryFieldLength)
        {
            return "repository.downloadIp exceeds max length.";
        }

        if (string.IsNullOrWhiteSpace(repository.FtpFolderPath))
        {
            return "repository.ftpFolderPath is required.";
        }

        if (repository.FtpFolderPath.Length > WindowsScreenSaverModuleConstants.MaxFtpFolderPathLength)
        {
            return "repository.ftpFolderPath exceeds max length.";
        }

        if (string.IsNullOrWhiteSpace(repository.ProtocolType))
        {
            return "repository.protocolType is required.";
        }

        if (string.IsNullOrWhiteSpace(repository.ConnectionName))
        {
            return "repository.connectionName is required.";
        }

        if (repository.FtpPassword.Length > WindowsScreenSaverModuleConstants.MaxFtpPasswordLength)
        {
            return "repository.ftpPassword exceeds max length.";
        }

        if (repository.Port is < 0 or > 65535)
        {
            return "repository.port must be between 0 and 65535.";
        }

        return null;
    }

    private static string SerializeBrowsePayload(WindowsScreenSaverSettingsRequest settings, int agentAction)
    {
        var screenSaver = new
        {
            intScreenSaverTimeOut = settings.TimeoutMinutes,
            blScreenSaverPasswordProtected = settings.PasswordProtected,
            strCurrentScreenSaver = settings.ScreenSaverName.Trim(),
            blUpload = false,
            ConnectionId = 0,
            DownloadIP = string.Empty,
            FTPFolderPath = string.Empty,
            FTPpassword = string.Empty,
            FTPSSLType = string.Empty,
            FTPUsername = string.Empty,
            LoggedInUserID = 0,
            Port = 0,
            ProtocolType = string.Empty,
            RepositoryType = "Browse",
            strText1 = settings.PreventUserChanges ? "true" : "false",
            strText2 = string.Empty,
            strText3 = string.Empty,
            strText4 = string.Empty,
            strText5 = string.Empty,
            TaskID = 0L,
            AgentAction = agentAction,
            ConnectionName = string.Empty
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPScreenSaver = screenSaver } });
    }
}
