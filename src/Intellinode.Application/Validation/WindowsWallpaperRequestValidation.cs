using System.Text.Json;
using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Validation;

/// <summary>
/// PR0 spike / PR3 repository validation shared by FluentValidation and the settings service.
/// </summary>
public static class WindowsWallpaperRequestValidation
{
    public const int MaxFunctionParameterLength = WindowsWallpaperModuleConstants.MaxFunctionParameterLength;

    public static bool IsRepositoryPath(WindowsWallpaperSettingsRequest settings) =>
        settings.Upload
        || string.Equals(settings.SourceType?.Trim(), "Repository", StringComparison.OrdinalIgnoreCase)
        || string.Equals(settings.SourceType?.Trim(), "Upload", StringComparison.OrdinalIgnoreCase);

    public static bool PayloadWithinLimit(WindowsWallpaperSettingsRequest settings, int agentAction)
    {
        if (IsRepositoryPath(settings))
        {
            return JsonSerializer.Serialize(new { settingsVersion = 1L }).Length <= MaxFunctionParameterLength;
        }

        return SerializeBrowsePayload(settings, agentAction).Length <= MaxFunctionParameterLength;
    }

    public static string? ValidateBrowseOnlySettings(WindowsWallpaperSettingsRequest settings)
    {
        if (IsRepositoryPath(settings))
        {
            return "Upload and Repository sources are not supported until PR3.";
        }

        if (settings.Upload)
        {
            return "upload must be false for browse path.";
        }

        if (settings.Repository is not null)
        {
            return "repository must be omitted for browse path.";
        }

        if (string.IsNullOrWhiteSpace(settings.PicturePath))
        {
            return "picturePath is required for browse path.";
        }

        if (settings.PicturePath.Trim().Length > WindowsWallpaperModuleConstants.MaxPicturePathLength)
        {
            return "picturePath exceeds max length.";
        }

        var sourceType = settings.SourceType?.Trim();
        if (!string.IsNullOrWhiteSpace(sourceType)
            && !string.Equals(sourceType, "Browse", StringComparison.OrdinalIgnoreCase))
        {
            return "sourceType must be Browse for PR2 browse apply.";
        }

        return ValidatePicturePosition(settings.PicturePosition);
    }

    public static string? ValidateRepositorySettings(WindowsWallpaperSettingsRequest settings)
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
                && !WindowsWallpaperModuleConstants.AllowedSourceTypes.Contains(sourceType, StringComparer.OrdinalIgnoreCase))
            {
                return "sourceType must be one of Browse, Upload, Repository.";
            }

            if (string.IsNullOrWhiteSpace(settings.PicturePath))
            {
                return "picturePath is required for browse path.";
            }

            if (settings.PicturePath.Trim().Length > WindowsWallpaperModuleConstants.MaxPicturePathLength)
            {
                return "picturePath exceeds max length.";
            }

            return ValidatePicturePosition(settings.PicturePosition);
        }

        if (settings.Repository is null)
        {
            return "repository is required for upload/repository path.";
        }

        if (string.IsNullOrWhiteSpace(settings.PictureName))
        {
            return "pictureName is required for upload/repository path.";
        }

        if (settings.PictureName.Trim().Length > WindowsWallpaperModuleConstants.MaxPictureNameLength)
        {
            return "pictureName exceeds max length.";
        }

        var repositoryError = ValidateRepository(settings.Repository);
        if (repositoryError is not null)
        {
            return repositoryError;
        }

        return ValidatePicturePosition(settings.PicturePosition);
    }

    public static string? ValidatePicturePosition(string? picturePosition)
    {
        if (string.IsNullOrWhiteSpace(picturePosition))
        {
            return "picturePosition is required.";
        }

        if (!WindowsWallpaperModuleConstants.AllowedPicturePositions.Contains(
                picturePosition.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            return "picturePosition must be one of Stretch, Tile, Center.";
        }

        return null;
    }

    public static string? ValidateRepository(WindowsWallpaperRepositoryRequest repository)
    {
        if (string.IsNullOrWhiteSpace(repository.DownloadIp))
        {
            return "repository.downloadIp is required.";
        }

        if (repository.DownloadIp.Length > WindowsWallpaperModuleConstants.MaxRepositoryFieldLength)
        {
            return "repository.downloadIp exceeds max length.";
        }

        if (string.IsNullOrWhiteSpace(repository.FtpFolderPath))
        {
            return "repository.ftpFolderPath is required.";
        }

        if (repository.FtpFolderPath.Length > WindowsWallpaperModuleConstants.MaxFtpFolderPathLength)
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

        if (repository.FtpPassword.Length > WindowsWallpaperModuleConstants.MaxFtpPasswordLength)
        {
            return "repository.ftpPassword exceeds max length.";
        }

        if (repository.Port is < 0 or > 65535)
        {
            return "repository.port must be between 0 and 65535.";
        }

        return null;
    }

    private static string SerializeBrowsePayload(WindowsWallpaperSettingsRequest settings, int agentAction)
    {
        var wallpaper = new
        {
            blUpload = false,
            strPictureName = settings.PicturePath.Trim(),
            strPicturePosition = settings.PicturePosition.Trim(),
            ProtocolType = string.Empty,
            ConnectionId = 0,
            FTPSSLType = string.Empty,
            Port = 0,
            RepositoryType = "Browse",
            DownloadIP = string.Empty,
            FTPFolderPath = string.Empty,
            FTPpassword = string.Empty,
            FTPUsername = string.Empty,
            LoggedInUserID = 0,
            strText1 = settings.PreventUserChanges ? "true" : "false",
            strText2 = string.Empty,
            strText3 = string.Empty,
            strText4 = string.Empty,
            strText5 = string.Empty,
            TaskID = 0L,
            AgentAction = agentAction,
            ConnectionName = string.Empty
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPWallPaper = wallpaper } });
    }
}
