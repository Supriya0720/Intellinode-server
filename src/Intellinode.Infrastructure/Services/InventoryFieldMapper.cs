using System.Text.Json;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Domain.Entities;

namespace Intellinode.Infrastructure.Services;

public static class InventoryFieldMapper
{
    public static void ApplyToDevice(Device device, AgentInventoryRequest request)
    {
        ApplyOsInfo(device, request.OsInfo);
        ApplyNetwork(device, request.Network);
        ApplyHardware(device, request.Hardware);
    }

    private static void ApplyOsInfo(Device device, JsonElement? osInfo)
    {
        if (!TryGetElement(osInfo, out var element))
        {
            return;
        }

        var os = GetStringProperty(element, "name");
        if (!string.IsNullOrWhiteSpace(os))
        {
            device.Os = os;
        }

        var version = GetStringProperty(element, "version");
        if (!string.IsNullOrWhiteSpace(version))
        {
            device.OsVersion = version;
        }

        var agentVersion = GetStringProperty(element, "agentVersion");
        if (!string.IsNullOrWhiteSpace(agentVersion))
        {
            device.AgentVersion = agentVersion;
        }
    }

    private static void ApplyNetwork(Device device, JsonElement? network)
    {
        if (!TryGetElement(network, out var element))
        {
            return;
        }

        var hostName = GetStringProperty(element, "hostName", "hostname");
        if (!string.IsNullOrWhiteSpace(hostName))
        {
            device.HostName = hostName;
        }

        var ipAddress = GetStringProperty(element, "ipAddress", "ip");
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            device.IpAddress = ipAddress;
        }
    }

    private static void ApplyHardware(Device device, JsonElement? hardware)
    {
        if (!TryGetElement(hardware, out _))
        {
            return;
        }

        // Hardware fields are stored in inventory JSON only; no required Device columns today.
    }

    private static bool TryGetElement(JsonElement? element, out JsonElement value)
    {
        if (!element.HasValue ||
            element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            value = default;
            return false;
        }

        value = element.Value;
        return value.ValueKind == JsonValueKind.Object;
    }

    private static string? GetStringProperty(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => null
                };
            }
        }

        return null;
    }
}
