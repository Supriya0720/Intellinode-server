using System.Text.RegularExpressions;
using System.Text.Json;
using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Validation;

/// <summary>
/// PR0 spike / PR2 validation shared by FluentValidation and the settings service.
/// </summary>
public static partial class WindowsApplicationCommandRequestValidation
{
    public const int MaxFunctionParameterLength = WindowsApplicationCommandModuleConstants.MaxFunctionParameterLength;

    private static readonly Regex ApplicationPathPattern = ApplicationPathRegex();

    public static bool PayloadWithinLimit(WindowsApplicationCommandSettingsRequest settings, int agentAction)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return SerializePayload(MapToPayloadRequest(settings, 42, agentAction)).Length <= MaxFunctionParameterLength;
    }

    public static bool PayloadWithinLimit(WindowsApplicationCommandPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SerializePayload(request).Length <= MaxFunctionParameterLength;
    }

    public static string? ValidateSettings(
        WindowsApplicationCommandSettingsRequest settings,
        WindowsApplicationCommandValidationPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        policy ??= new WindowsApplicationCommandValidationPolicy();

        var mode = settings.Mode?.Trim() ?? string.Empty;
        if (!string.Equals(mode, WindowsApplicationCommandModuleConstants.ApplicationModuleName, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, WindowsApplicationCommandModuleConstants.CommandModuleName, StringComparison.OrdinalIgnoreCase))
        {
            return "mode must be Application or Command.";
        }

        if (string.Equals(mode, WindowsApplicationCommandModuleConstants.ApplicationModuleName, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateApplicationSettings(settings);
        }

        return ValidateCommandSettings(settings, policy);
    }

    public static WindowsApplicationCommandPayloadRequest MapToPayloadRequest(
        WindowsApplicationCommandSettingsRequest settings,
        long taskId,
        int agentAction)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var mode = WindowsApplicationCommandModuleConstants.ParseMode(settings.Mode);
        if (mode == WindowsApplicationCommandMode.Command)
        {
            return new WindowsApplicationCommandPayloadRequest
            {
                Mode = WindowsApplicationCommandMode.Command,
                CommandText = settings.CommandText.Trim(),
                Timeout = settings.Timeout.Trim(),
                RebootRequired = settings.RebootRequired,
                RequireCommandOutput = settings.RequireCommandOutput,
                TaskId = taskId,
                AgentAction = agentAction
            };
        }

        return new WindowsApplicationCommandPayloadRequest
        {
            Mode = WindowsApplicationCommandMode.Application,
            ApplicationPath = settings.ApplicationPath.Trim(),
            Parameters = settings.Parameters.Trim(),
            WarnUser = settings.WarnUser,
            AlertTitle = settings.AlertTitle.Trim(),
            AlertMessage = settings.AlertMessage.Trim(),
            MessageType = settings.MessageType.Trim(),
            DisplayTime = settings.DisplayTime.Trim(),
            RebootRequired = settings.RebootRequired,
            TaskId = taskId,
            AgentAction = agentAction
        };
    }

    public static string SerializePayload(WindowsApplicationCommandPayloadRequest request) =>
        SerializePayloadInternal(request);

    private static string? ValidateApplicationSettings(WindowsApplicationCommandSettingsRequest settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApplicationPath))
        {
            return "applicationPath is required for Application mode.";
        }

        if (settings.ApplicationPath.Trim().Length > WindowsApplicationCommandModuleConstants.MaxApplicationPathLength)
        {
            return "applicationPath exceeds max length.";
        }

        if (!IsValidApplicationPath(settings.ApplicationPath))
        {
            return "applicationPath must be a valid drive path ending with .exe (e.g. C:\\Program Files\\App\\app.exe).";
        }

        if (settings.Parameters.Trim().Length > WindowsApplicationCommandModuleConstants.MaxParametersLength)
        {
            return "parameters exceeds max length.";
        }

        if (settings.WarnUser)
        {
            if (string.IsNullOrWhiteSpace(settings.AlertTitle))
            {
                return "alertTitle is required when warnUser is true.";
            }

            if (string.IsNullOrWhiteSpace(settings.AlertMessage))
            {
                return "alertMessage is required when warnUser is true.";
            }

            if (string.IsNullOrWhiteSpace(settings.MessageType))
            {
                return "messageType is required when warnUser is true.";
            }

            if (string.IsNullOrWhiteSpace(settings.DisplayTime))
            {
                return "displayTime is required when warnUser is true.";
            }

            if (!WindowsApplicationCommandReferenceCatalog.IsValidMessageType(settings.MessageType))
            {
                return "messageType must be a supported reference value (0 or 1).";
            }

            if (!WindowsApplicationCommandReferenceCatalog.IsValidDisplayTime(settings.DisplayTime))
            {
                return "displayTime must be a supported reference value.";
            }
        }
        else
        {
            var referenceError = ValidateOptionalApplicationReferenceFields(settings);
            if (referenceError is not null)
            {
                return referenceError;
            }
        }

        return ValidateSharedFieldLengths(settings);
    }

    private static string? ValidateCommandSettings(
        WindowsApplicationCommandSettingsRequest settings,
        WindowsApplicationCommandValidationPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(settings.CommandText))
        {
            return "commandText is required for Command mode.";
        }

        if (settings.CommandText.Trim().Length > WindowsApplicationCommandModuleConstants.MaxCommandTextLength)
        {
            return "commandText exceeds max length.";
        }

        if (WindowsApplicationCommandReferenceCatalog.IsDeniedCommand(settings.CommandText, policy))
        {
            return "Command is denied by policy.";
        }

        if (settings.Timeout.Trim().Length > WindowsApplicationCommandModuleConstants.MaxTimeoutLength)
        {
            return "timeout exceeds max length.";
        }

        if (!string.IsNullOrWhiteSpace(settings.Timeout)
            && !WindowsApplicationCommandReferenceCatalog.IsValidTimeout(settings.Timeout))
        {
            return "timeout must be a supported reference value.";
        }

        return ValidateSharedFieldLengths(settings);
    }

    private static string? ValidateOptionalApplicationReferenceFields(WindowsApplicationCommandSettingsRequest settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.MessageType)
            && !WindowsApplicationCommandReferenceCatalog.IsValidMessageType(settings.MessageType))
        {
            return "messageType must be a supported reference value (0 or 1).";
        }

        if (!string.IsNullOrWhiteSpace(settings.DisplayTime)
            && !WindowsApplicationCommandReferenceCatalog.IsValidDisplayTime(settings.DisplayTime))
        {
            return "displayTime must be a supported reference value.";
        }

        return null;
    }

    private static string? ValidateSharedFieldLengths(WindowsApplicationCommandSettingsRequest settings)
    {
        if (settings.AlertTitle.Trim().Length > WindowsApplicationCommandModuleConstants.MaxAlertTitleLength)
        {
            return "alertTitle exceeds max length.";
        }

        if (settings.AlertMessage.Trim().Length > WindowsApplicationCommandModuleConstants.MaxAlertMessageLength)
        {
            return "alertMessage exceeds max length.";
        }

        if (settings.MessageType.Trim().Length > WindowsApplicationCommandModuleConstants.MaxMessageTypeLength)
        {
            return "messageType exceeds max length.";
        }

        if (settings.DisplayTime.Trim().Length > WindowsApplicationCommandModuleConstants.MaxDisplayTimeLength)
        {
            return "displayTime exceeds max length.";
        }

        return null;
    }

    public static bool IsValidApplicationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();
        if (trimmed.Length < 4)
        {
            return false;
        }

        if (!ApplicationPathPattern.IsMatch(trimmed))
        {
            return false;
        }

        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string SerializePayloadInternal(WindowsApplicationCommandPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Mode == WindowsApplicationCommandMode.Command)
        {
            var command = new
            {
                strCommand = request.CommandText,
                TimeOut = request.Timeout,
                Text1 = request.RebootRequired ? "1" : "0",
                Text2 = request.RequireCommandOutput ? "1" : "0",
                Text3 = string.Empty,
                Text4 = string.Empty,
                Text5 = string.Empty,
                TaskID = request.TaskId,
                AgentAction = request.AgentAction
            };

            return JsonSerializer.Serialize(new { WinCELinux = new { Command = command } });
        }

        var application = new
        {
            ApplicationPath = request.ApplicationPath,
            Parameter = request.Parameters,
            IsWarnUser = request.WarnUser,
            Title = request.AlertTitle,
            Message = request.AlertMessage,
            MessageType = request.MessageType,
            DisplayTime = request.DisplayTime,
            Text1 = request.RebootRequired ? "1" : "0",
            Text2 = string.Empty,
            Text3 = string.Empty,
            Text4 = string.Empty,
            Text5 = string.Empty,
            TaskID = request.TaskId,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { Application = application } });
    }

    [GeneratedRegex(@"^[A-Za-z]:\\", RegexOptions.CultureInvariant)]
    private static partial Regex ApplicationPathRegex();
}
