using System.Text.Json;
using System.Text.Json.Nodes;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;

namespace Intellinode.Infrastructure.Services;

public static class WindowsWirelessPropertiesPayloadShape
{
    /// <summary>
    /// FusionX delete DAC parity: only <c>strNetworkSSDIName</c> populated; other fields empty/default.
    /// </summary>
    public static bool IsDeleteShapeInnerSettingsJson(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("strNetworkSSDIName", out var ssidElement) ||
                ssidElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(ssidElement.GetString()))
            {
                return false;
            }

            return IsEmptyStringProperty(root, "strNetworkAuthentication") &&
                   IsEmptyStringProperty(root, "strNetworkDataEncr") &&
                   IsEmptyStringProperty(root, "strNetworkKey") &&
                   IsEmptyStringProperty(root, "strNetworkPPK") &&
                   IsEmptyStringProperty(root, "strNetworkName") &&
                   IsEmptyStringProperty(root, "strStatus") &&
                   IsEmptyStringProperty(root, "Text1") &&
                   IsEmptyStringProperty(root, "Text2") &&
                   IsEmptyStringProperty(root, "Text3") &&
                   (!root.TryGetProperty("iNetworkKeyIndex", out var keyIndex) ||
                    keyIndex.ValueKind == JsonValueKind.Number && keyIndex.GetInt32() == 0) &&
                   (!root.TryGetProperty("Conn_Auto_WhenIn_Range", out var autoConnect) ||
                    autoConnect.ValueKind == JsonValueKind.False);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryExtractSsidFromInnerSettingsJson(string? settingsJson, out string? ssid)
    {
        ssid = null;
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            if (!document.RootElement.TryGetProperty("strNetworkSSDIName", out var ssidElement) ||
                ssidElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            ssid = ssidElement.GetString();
            return !string.IsNullOrWhiteSpace(ssid);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsEmptyStringProperty(JsonElement root, string propertyName)
    {
        return !root.TryGetProperty(propertyName, out var element) ||
               element.ValueKind == JsonValueKind.Null ||
               (element.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(element.GetString()));
    }
}

public sealed class WindowsWirelessPropertiesPayloadBuilder : IWindowsWirelessPropertiesPayloadBuilder
{
    public const int MaxCompactTaskReferenceLength = 64;

    private static readonly JsonSerializerOptions SerializerOptions = new();

    public string BuildAgentPayload(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            throw new ArgumentException("Settings JSON is required.", nameof(settingsJson));
        }

        var inner = JsonNode.Parse(settingsJson);
        if (inner is not JsonObject)
        {
            throw new ArgumentException("Settings JSON must be a JSON object.", nameof(settingsJson));
        }

        var wrapper = new JsonObject
        {
            ["WinCELinux"] = new JsonObject
            {
                ["XPWirelessNetworkSecuritySettings"] = inner
            }
        };

        return wrapper.ToJsonString(SerializerOptions);
    }

    public string BuildCompactTaskReference(long settingsVersion, long profileKey)
    {
        if (settingsVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settingsVersion), "Settings version must be non-negative.");
        }

        if (profileKey <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(profileKey), "Profile key must be positive.");
        }

        var reference = JsonSerializer.Serialize(new { settingsVersion, profileKey }, SerializerOptions);
        if (reference.Length > MaxCompactTaskReferenceLength)
        {
            throw new InvalidOperationException(
                $"Compact task reference exceeds {MaxCompactTaskReferenceLength} characters ({reference.Length}).");
        }

        return reference;
    }

    public bool TryParseCompactTaskReference(
        string functionParameter,
        out long settingsVersion,
        out long profileKey)
    {
        settingsVersion = 0;
        profileKey = 0;
        if (string.IsNullOrWhiteSpace(functionParameter))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(functionParameter);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("settingsVersion", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt64(out settingsVersion) ||
                !root.TryGetProperty("profileKey", out var profileKeyElement) ||
                profileKeyElement.ValueKind != JsonValueKind.Number ||
                !profileKeyElement.TryGetInt64(out profileKey))
            {
                return false;
            }

            return settingsVersion >= 0 && profileKey > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public string BuildInnerSettingsJson(
        WindowsWirelessPropertiesProfileRequest profile,
        WirelessProfileOperation operation)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (operation == WirelessProfileOperation.Delete)
        {
            return JsonSerializer.Serialize(new
            {
                strNetworkSSDIName = profile.Ssid,
                strNetworkAuthentication = string.Empty,
                strNetworkDataEncr = string.Empty,
                strNetworkKey = string.Empty,
                strNetworkPPK = string.Empty,
                iNetworkKeyIndex = 0,
                strNetworkName = string.Empty,
                strStatus = string.Empty,
                Conn_Auto_WhenIn_Range = false,
                Text1 = string.Empty,
                Text2 = string.Empty,
                Text3 = string.Empty,
                TaskID = 0,
                AgentAction = 0
            }, SerializerOptions);
        }

        return JsonSerializer.Serialize(new
        {
            strNetworkSSDIName = profile.Ssid,
            strNetworkAuthentication = profile.NetworkAuthentication,
            strNetworkDataEncr = profile.DataEncryption,
            strNetworkKey = profile.NetworkKey,
            strNetworkPPK = profile.PreSharedKey,
            iNetworkKeyIndex = profile.KeyIndex,
            strNetworkName = profile.NetworkName,
            strStatus = profile.Status,
            Conn_Auto_WhenIn_Range = profile.ConnectWhenInRange,
            Text1 = profile.ConnectNonBroadcasting ? "true" : "false",
            Text2 = profile.Text2,
            Text3 = profile.Text3,
            TaskID = 0,
            AgentAction = 0
        }, SerializerOptions);
    }
}
