using System.Text.Json;
using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Validation;

public static class WindowsTaskbarRequestValidation
{
    public const int MaxFunctionParameterLength = WindowsTaskbarModuleConstants.MaxFunctionParameterLength;

    public static bool PayloadWithinLimit(WindowsTaskbarSettingsRequest settings, int agentAction) =>
        SerializePayload(settings, agentAction).Length <= MaxFunctionParameterLength;

    private static string SerializePayload(WindowsTaskbarSettingsRequest settings, int agentAction)
    {
        var taskbar = new
        {
            blTaskbarLock = settings.LockTaskbar,
            blAutoHideTaskbar = settings.AutoHideTaskbar,
            blKeepTaskbarOnTop = settings.KeepTaskbarOnTop,
            blGroupSimillarTaskbarButtons = settings.GroupSimilarButtons,
            blShowQuckLaunch = settings.ShowQuickLaunch,
            TaskID = 0L,
            AgentAction = agentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPTaskbarProperties = taskbar } });
    }
}
