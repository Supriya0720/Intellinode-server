using System.Text.Json;
using Intellinode.Application.Contracts.Agents;

namespace Intellinode.Infrastructure.Services;

internal static class WindowsTaskbarLivePayloadParser
{
    internal sealed record ParsedLiveSettings(
        bool LockTaskbar,
        bool AutoHideTaskbar,
        bool KeepTaskbarOnTop,
        bool GroupSimilarButtons,
        bool ShowQuickLaunch,
        bool ShowClock,
        bool HideInactiveIcons);

    public static bool TryParse(AgentTaskbarLiveReportRequest request, out ParsedLiveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.WinCELinux is { } wrapper &&
            TryParseFusionXWrapper(wrapper, out settings))
        {
            return true;
        }

        if (HasFlatValues(request))
        {
            settings = new ParsedLiveSettings(
                request.LockTaskbar ?? true,
                request.AutoHideTaskbar ?? false,
                request.KeepTaskbarOnTop ?? true,
                request.GroupSimilarButtons ?? true,
                request.ShowQuickLaunch ?? false,
                request.ShowClock ?? false,
                request.HideInactiveIcons ?? false);
            return true;
        }

        settings = default!;
        return false;
    }

    private static bool HasFlatValues(AgentTaskbarLiveReportRequest request) =>
        request.LockTaskbar.HasValue ||
        request.AutoHideTaskbar.HasValue ||
        request.KeepTaskbarOnTop.HasValue ||
        request.GroupSimilarButtons.HasValue ||
        request.ShowQuickLaunch.HasValue ||
        request.ShowClock.HasValue ||
        request.HideInactiveIcons.HasValue;

    private static bool TryParseFusionXWrapper(JsonElement wrapper, out ParsedLiveSettings settings)
    {
        settings = default!;

        if (!TryGetTaskbarProperties(wrapper, out var taskbar))
        {
            return false;
        }

        settings = new ParsedLiveSettings(
            ReadBool(taskbar, "blTaskbarLock", "LockTaskbar", defaultValue: true),
            ReadBool(taskbar, "blAutoHideTaskbar", "AutoHideTaskbar"),
            ReadBool(taskbar, "blKeepTaskbarOnTop", "KeepTaskbarOnTop", defaultValue: true),
            ReadBool(taskbar, "blGroupSimillarTaskbarButtons", "GroupSimilarButtons", "GroupSimillarButtons", defaultValue: true),
            ReadBool(taskbar, "blShowQuckLaunch", "ShowQuickLaunch"),
            ReadBool(taskbar, "blShowClock", "ShowClock"),
            ReadBool(taskbar, "blHideInactiveIcons", "HideInactiveIcons"));

        return true;
    }

    private static bool TryGetTaskbarProperties(JsonElement wrapper, out JsonElement taskbar)
    {
        if (wrapper.TryGetProperty("XPTaskbarProperties", out taskbar))
        {
            return true;
        }

        if (wrapper.TryGetProperty("WinCELinux", out var nested) &&
            nested.TryGetProperty("XPTaskbarProperties", out taskbar))
        {
            return true;
        }

        taskbar = default;
        return false;
    }

    private static bool ReadBool(JsonElement element, string primaryName, string secondaryName, string? tertiaryName = null, bool defaultValue = false)
    {
        if (TryReadBoolProperty(element, primaryName, out var value))
        {
            return value;
        }

        if (TryReadBoolProperty(element, secondaryName, out value))
        {
            return value;
        }

        if (tertiaryName is not null && TryReadBoolProperty(element, tertiaryName, out value))
        {
            return value;
        }

        return defaultValue;
    }

    private static bool TryReadBoolProperty(JsonElement element, string propertyName, out bool value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        switch (property.ValueKind)
        {
            case JsonValueKind.True:
                value = true;
                return true;
            case JsonValueKind.False:
                value = false;
                return true;
            case JsonValueKind.String when bool.TryParse(property.GetString(), out var parsed):
                value = parsed;
                return true;
            case JsonValueKind.Number when property.TryGetInt32(out var number):
                value = number != 0;
                return true;
            default:
                return false;
        }
    }
}
