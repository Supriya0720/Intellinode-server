using System.Text.Json;
using System.Text.Json.Nodes;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Builds FusionX-shaped power management agent payloads.
/// FusionX DAC <c>getPowerSettings</c> strips the <c>" Minutes"</c> suffix when values contain
/// <c>Minutes</c> (numeric prefix only). UI labels such as <c>Never</c> pass through unchanged.
/// </summary>
public sealed class WindowsPowerManagementPayloadBuilder : IWindowsPowerManagementPayloadBuilder
{
    public const int MaxCompactTaskReferenceLength = 128;

    private static readonly JsonSerializerOptions SerializerOptions = new();

    public string BuildAgentPayload(string settingsJson, long legacyTaskId, int agentAction)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            throw new ArgumentException("Settings JSON is required.", nameof(settingsJson));
        }

        var inner = JsonNode.Parse(settingsJson);
        if (inner is not JsonObject innerObject)
        {
            throw new ArgumentException("Settings JSON must be a JSON object.", nameof(settingsJson));
        }

        innerObject["TaskID"] = legacyTaskId;
        innerObject["AgentAction"] = agentAction;

        var wrapper = new JsonObject
        {
            ["WinCELinux"] = new JsonObject
            {
                ["XPPowerManagement"] = innerObject.DeepClone()
            }
        };

        return wrapper.ToJsonString(SerializerOptions);
    }

    public string BuildCompactTaskReference(long settingsVersion, string? planName = null)
    {
        if (settingsVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settingsVersion), "Settings version must be non-negative.");
        }

        object reference = string.IsNullOrWhiteSpace(planName)
            ? new { settingsVersion }
            : new { settingsVersion, planName };

        var json = JsonSerializer.Serialize(reference, SerializerOptions);
        if (json.Length > WindowsPowerManagementModuleConstants.MaxFunctionParameterLength)
        {
            throw new InvalidOperationException(
                $"Compact task reference exceeds {WindowsPowerManagementModuleConstants.MaxFunctionParameterLength} characters ({json.Length}).");
        }

        if (json.Length > MaxCompactTaskReferenceLength)
        {
            throw new InvalidOperationException(
                $"Compact task reference exceeds recommended length {MaxCompactTaskReferenceLength} characters ({json.Length}).");
        }

        return json;
    }

    public bool TryParseCompactTaskReference(string stored, out long settingsVersion, out string? planName)
    {
        settingsVersion = 0;
        planName = null;

        if (string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(stored);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("settingsVersion", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt64(out settingsVersion))
            {
                return false;
            }

            if (root.TryGetProperty("planName", out var planElement) &&
                planElement.ValueKind == JsonValueKind.String)
            {
                planName = planElement.GetString();
            }

            return settingsVersion >= 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public string BuildSettingsJsonFromBasic(WindowsPowerManagementBasicSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SerializeSettingsDocument(
            request.PlanName,
            request.IsActive,
            NormalizeGroups(request.OptionGroups),
            request.Operation,
            request.Index,
            includeExtendedFields: false);
    }

    public string MergeAdvancedSettingsJson(string? existingSettingsJson, WindowsPowerManagementAdvancedSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existingGroups = ParseOptionGroups(existingSettingsJson, out var operation, out var index, out var text3, out var text4);
        var merged = MergeOptionGroups(existingGroups, NormalizeGroups(request.OptionGroups));
        var includeExtended = WindowsPowerManagementCatalog.ContainsAdvancedOption(merged) ||
                              !string.IsNullOrEmpty(text3) ||
                              !string.IsNullOrEmpty(text4);

        return SerializeSettingsDocument(
            request.PlanName,
            request.IsActive,
            merged,
            operation,
            index,
            includeExtendedFields: includeExtended,
            strText3: text3,
            strText4: text4);
    }

    public string NormalizeSettingValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        if (trimmed.Contains("Minutes", StringComparison.OrdinalIgnoreCase))
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out _))
            {
                return parts[0];
            }
        }

        return trimmed;
    }

    public string BuildExtraData(string macAddress, string planName, string? signalSuffix = null)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            throw new ArgumentException("MAC address is required.", nameof(macAddress));
        }

        if (string.IsNullOrWhiteSpace(planName))
        {
            throw new ArgumentException("Plan name is required.", nameof(planName));
        }

        var suffix = string.IsNullOrWhiteSpace(signalSuffix)
            ? WindowsPowerManagementModuleConstants.DefaultSignalSuffix
            : signalSuffix.Trim();

        var extraData = $"{macAddress.Trim()}&{suffix},{planName.Trim()}";
        if (extraData.Length > WindowsPowerManagementModuleConstants.MaxFunctionParameterLength)
        {
            throw new InvalidOperationException(
                $"ExtraData exceeds {WindowsPowerManagementModuleConstants.MaxFunctionParameterLength} characters ({extraData.Length}).");
        }

        return extraData;
    }

    internal static List<WindowsPowerManagementOptionGroup> MergeOptionGroups(
        List<WindowsPowerManagementOptionGroup> existingGroups,
        List<WindowsPowerManagementOptionGroup> incomingGroups)
    {
        var merged = existingGroups
            .Select(CloneGroup)
            .ToList();

        foreach (var incoming in incomingGroups)
        {
            var match = merged.FirstOrDefault(g =>
                string.Equals(g.OptionName, incoming.OptionName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                merged.Add(CloneGroup(incoming));
                continue;
            }

            foreach (var setting in incoming.Settings)
            {
                var existingSetting = match.Settings.FirstOrDefault(s =>
                    string.Equals(s.SettingName, setting.SettingName, StringComparison.OrdinalIgnoreCase));
                if (existingSetting is null)
                {
                    match.Settings.Add(new WindowsPowerManagementSettingValue
                    {
                        SettingName = setting.SettingName,
                        SettingValue = setting.SettingValue
                    });
                }
                else
                {
                    existingSetting.SettingValue = setting.SettingValue;
                }
            }
        }

        return merged;
    }

    private List<WindowsPowerManagementOptionGroup> NormalizeGroups(IEnumerable<WindowsPowerManagementOptionGroup> groups) =>
        groups.Select(group => new WindowsPowerManagementOptionGroup
        {
            OptionName = group.OptionName.Trim(),
            Settings = group.Settings
                .Where(s => !string.IsNullOrWhiteSpace(s.SettingName))
                .Select(setting => new WindowsPowerManagementSettingValue
                {
                    SettingName = setting.SettingName.Trim(),
                    SettingValue = NormalizeSettingValue(setting.SettingValue)
                })
                .ToList()
        }).Where(g => g.Settings.Count > 0).ToList();

    private static List<WindowsPowerManagementOptionGroup> ParseOptionGroups(
        string? existingSettingsJson,
        out string operation,
        out string index,
        out string? strText3,
        out string? strText4)
    {
        operation = "Update";
        index = "1";
        strText3 = null;
        strText4 = null;

        if (string.IsNullOrWhiteSpace(existingSettingsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(existingSettingsJson);
            var root = document.RootElement;
            if (root.TryGetProperty("Operation", out var operationElement))
            {
                operation = operationElement.GetString() ?? operation;
            }

            if (root.TryGetProperty("Index", out var indexElement))
            {
                index = indexElement.GetString() ?? index;
            }

            if (root.TryGetProperty("strText3", out var text3Element) &&
                text3Element.ValueKind == JsonValueKind.String)
            {
                strText3 = text3Element.GetString();
            }

            if (root.TryGetProperty("strText4", out var text4Element) &&
                text4Element.ValueKind == JsonValueKind.String)
            {
                strText4 = text4Element.GetString();
            }

            if (!root.TryGetProperty("objPowerOptions", out var optionsElement) ||
                optionsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var groups = new List<WindowsPowerManagementOptionGroup>();
            foreach (var option in optionsElement.EnumerateArray())
            {
                if (!option.TryGetProperty("strPowerOptionName", out var optionNameElement) ||
                    !option.TryGetProperty("objPowerSettings", out var settingsElement) ||
                    settingsElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var group = new WindowsPowerManagementOptionGroup
                {
                    OptionName = optionNameElement.GetString() ?? string.Empty
                };

                foreach (var setting in settingsElement.EnumerateArray())
                {
                    group.Settings.Add(new WindowsPowerManagementSettingValue
                    {
                        SettingName = setting.TryGetProperty("strSettingName", out var nameElement)
                            ? nameElement.GetString() ?? string.Empty
                            : string.Empty,
                        SettingValue = setting.TryGetProperty("strSettingValue", out var valueElement)
                            ? valueElement.GetString() ?? string.Empty
                            : string.Empty
                    });
                }

                if (group.Settings.Count > 0)
                {
                    groups.Add(group);
                }
            }

            return groups;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private string SerializeSettingsDocument(
        string planName,
        bool isActive,
        List<WindowsPowerManagementOptionGroup> groups,
        string operation,
        string index,
        bool includeExtendedFields,
        string? strText3 = null,
        string? strText4 = null)
    {
        if (includeExtendedFields)
        {
            var extendedPayload = new
            {
                strPowerSchemaName = planName,
                blIsActive = isActive,
                objPowerOptions = groups.Select(group => new
                {
                    strPowerOptionName = group.OptionName,
                    objPowerSettings = group.Settings.Select(setting => new
                    {
                        strSettingName = setting.SettingName,
                        strSettingValue = setting.SettingValue
                    }).ToArray()
                }).ToArray(),
                Operation = operation,
                Index = index,
                strText3 = strText3 ?? string.Empty,
                strText4 = strText4 ?? string.Empty
            };

            return JsonSerializer.Serialize(extendedPayload, SerializerOptions);
        }

        var payload = new
        {
            strPowerSchemaName = planName,
            blIsActive = isActive,
            objPowerOptions = groups.Select(group => new
            {
                strPowerOptionName = group.OptionName,
                objPowerSettings = group.Settings.Select(setting => new
                {
                    strSettingName = setting.SettingName,
                    strSettingValue = setting.SettingValue
                }).ToArray()
            }).ToArray(),
            Operation = operation,
            Index = index
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static WindowsPowerManagementOptionGroup CloneGroup(WindowsPowerManagementOptionGroup group) =>
        new()
        {
            OptionName = group.OptionName,
            Settings = group.Settings.Select(s => new WindowsPowerManagementSettingValue
            {
                SettingName = s.SettingName,
                SettingValue = s.SettingValue
            }).ToList()
        };
}
