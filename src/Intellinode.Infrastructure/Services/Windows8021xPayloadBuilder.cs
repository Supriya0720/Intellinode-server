using System.Text.Json;
using System.Text.Json.Nodes;
using Intellinode.Application.Interfaces;

namespace Intellinode.Infrastructure.Services;

public sealed class Windows8021xPayloadBuilder : IWindows8021xPayloadBuilder
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
                ["Windows_802_1x"] = inner
            }
        };

        return wrapper.ToJsonString(SerializerOptions);
    }

    public string BuildCompactTaskReference(long settingsVersion)
    {
        if (settingsVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settingsVersion), "Settings version must be non-negative.");
        }

        var reference = JsonSerializer.Serialize(new { settingsVersion }, SerializerOptions);
        if (reference.Length > MaxCompactTaskReferenceLength)
        {
            throw new InvalidOperationException(
                $"Compact task reference exceeds {MaxCompactTaskReferenceLength} characters ({reference.Length}).");
        }

        return reference;
    }

    public bool TryParseCompactTaskReference(string functionParameter, out long settingsVersion)
    {
        settingsVersion = 0;
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
                !versionElement.TryGetInt64(out settingsVersion))
            {
                return false;
            }

            return settingsVersion >= 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
