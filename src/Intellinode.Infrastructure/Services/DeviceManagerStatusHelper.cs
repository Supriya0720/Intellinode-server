using System.Text.Json;
using Intellinode.Domain.Enums;

namespace Intellinode.Infrastructure.Services;

internal static class DeviceManagerStatusHelper
{
    public static readonly TimeSpan StaleHeartbeatThreshold = TimeSpan.FromMinutes(15);

    public static string MapDeviceStatus(
        EnrollmentState enrollmentState,
        bool isOnline,
        string clientStatus,
        DateTime? lastHeartbeatUtc)
    {
        if (enrollmentState == EnrollmentState.Disabled)
        {
            return "Maintenance";
        }

        if (IsDeviceOnline(isOnline, clientStatus))
        {
            return "Online";
        }

        if (lastHeartbeatUtc.HasValue &&
            lastHeartbeatUtc.Value < DateTime.UtcNow.Subtract(StaleHeartbeatThreshold))
        {
            return "Stale";
        }

        return "Offline";
    }

    public static bool IsDeviceOnline(bool isOnline, string clientStatus) =>
        isOnline && NormalizeClientStatus(clientStatus) == ClientPowerStatus.On;

    public static string NormalizeClientStatus(string clientStatus)
    {
        var normalized = clientStatus.Trim().ToUpperInvariant();
        if (!normalized.Contains('~'))
        {
            return normalized;
        }

        return normalized.Split('~', 2, StringSplitOptions.TrimEntries)[0];
    }

    public static string MapAgentType(string os)
    {
        if (string.IsNullOrWhiteSpace(os))
        {
            return string.Empty;
        }

        var normalized = os.Trim();
        if (normalized.Equals("Windows", StringComparison.OrdinalIgnoreCase))
        {
            return "Windows Agent";
        }

        if (normalized.Equals("Linux", StringComparison.OrdinalIgnoreCase))
        {
            return "Linux Agent";
        }

        if (normalized.Equals("macOS", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("darwin", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Mac", StringComparison.OrdinalIgnoreCase))
        {
            return "Mac Agent";
        }

        return $"{normalized} Agent";
    }

    public static int? TryParseBatteryPercent(string? hardwareJson)
    {
        if (string.IsNullOrWhiteSpace(hardwareJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(hardwareJson);
            var root = document.RootElement;

            if (root.TryGetProperty("batteryPercent", out var direct) &&
                direct.TryGetInt32(out var directValue))
            {
                return directValue;
            }

            if (root.TryGetProperty("battery", out var battery) &&
                battery.TryGetProperty("percent", out var percent) &&
                percent.TryGetInt32(out var nestedValue))
            {
                return nestedValue;
            }
        }
        catch (JsonException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public static bool DeviceStatusMatchesFilter(string deviceStatus, string statusFilter) =>
        deviceStatus.Equals(statusFilter, StringComparison.OrdinalIgnoreCase) ||
        (statusFilter.Equals("Offline", StringComparison.OrdinalIgnoreCase) &&
         deviceStatus.Equals("Stale", StringComparison.OrdinalIgnoreCase));

    public static JsonElement? TryParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
