using System.Text.Json;
using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Validation;

/// <summary>
/// Shared autologon validation and payload sizing for FluentValidation and the settings service.
/// </summary>
public static class WindowsUserInterfaceRequestValidation
{
    public const int MaxFunctionParameterLength = WindowsUserInterfaceModuleConstants.MaxFunctionParameterLength;

    public static bool PayloadWithinLimit(
        WindowsUserInterfaceSettingsRequest settings,
        int agentAction,
        bool useCompactTaskReference)
    {
        if (useCompactTaskReference)
        {
            return JsonSerializer.Serialize(new { settingsVersion = 1L }).Length <= MaxFunctionParameterLength;
        }

        return SerializeInlinePayload(settings, agentAction).Length <= MaxFunctionParameterLength;
    }

    public static string? ValidateAutologonCredentials(WindowsUserInterfaceSettingsRequest settings)
    {
        if (!settings.AutoLogon)
        {
            if (!string.IsNullOrEmpty(settings.Password))
            {
                return "password must be omitted when autoLogon is false.";
            }

            if (settings.KeepExistingPassword)
            {
                return "keepExistingPassword must be false when autoLogon is false.";
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(settings.UserName))
        {
            return "userName is required when autoLogon is true.";
        }

        if (settings.KeepExistingPassword && !string.IsNullOrEmpty(settings.Password))
        {
            return "password must be omitted when keepExistingPassword is true.";
        }

        if (!settings.KeepExistingPassword && string.IsNullOrEmpty(settings.Password))
        {
            return "password is required when autoLogon is true unless keepExistingPassword is true.";
        }

        return null;
    }

    private static string SerializeInlinePayload(WindowsUserInterfaceSettingsRequest settings, int agentAction)
    {
        var autologon = new
        {
            Name = settings.UserName.Trim(),
            Autologon = settings.AutoLogon,
            password = settings.Password ?? string.Empty,
            TaskID = 0L,
            AgentAction = agentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPAutologon = autologon } });
    }
}
