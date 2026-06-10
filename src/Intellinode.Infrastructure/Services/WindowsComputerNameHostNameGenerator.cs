using System.Text;
using System.Text.Json;
using Intellinode.Domain.Entities;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public static class WindowsComputerNameHostNameGenerator
{
    public const int NetBiosMaxLength = 15;
    public const int DefaultNoOfChar = 12;
    public const int MaxUniquenessAttempts = 20;

    public static bool HasAutoGenerateMetadata(string hostName, string prefix, string postfix, int noOfChar, bool isMacOrSerial) =>
        string.IsNullOrWhiteSpace(hostName) &&
        (isMacOrSerial ||
         !string.IsNullOrWhiteSpace(prefix) ||
         !string.IsNullOrWhiteSpace(postfix) ||
         noOfChar > 0);

    public static string GenerateHostName(
        Device device,
        string prefix,
        string postfix,
        int noOfChar,
        bool isMacOrSerial)
    {
        var effectiveNoOfChar = noOfChar <= 0 ? DefaultNoOfChar : Math.Clamp(noOfChar, 1, 15);
        string middle;
        if (isMacOrSerial)
        {
            middle = ExtractSegment(TryParseSerialFromHardwareJson(device.Inventory?.HardwareJson), effectiveNoOfChar);
            if (string.IsNullOrEmpty(middle))
            {
                middle = ExtractMacSegment(device.MacAddress, effectiveNoOfChar);
            }
        }
        else
        {
            middle = ExtractMacSegment(device.MacAddress, effectiveNoOfChar);
        }

        return TruncateNetBios(JoinHostNameParts(prefix, middle, postfix));
    }

    public static async Task<string?> EnsureUniqueHostNameAsync(
        IntellinodeDbContext dbContext,
        Guid tenantId,
        Guid deviceId,
        string candidateHostName,
        CancellationToken cancellationToken = default)
    {
        var baseName = TruncateNetBios(candidateHostName.Trim());
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return null;
        }

        if (!await HasHostNameCollisionAsync(dbContext, tenantId, deviceId, baseName, cancellationToken))
        {
            return baseName;
        }

        for (var attempt = 2; attempt <= MaxUniquenessAttempts + 1; attempt++)
        {
            var suffix = $"-{attempt}";
            var trimmedBase = baseName;
            if (trimmedBase.Length + suffix.Length > NetBiosMaxLength)
            {
                trimmedBase = trimmedBase[..Math.Max(1, NetBiosMaxLength - suffix.Length)];
            }

            var candidate = $"{trimmedBase}{suffix}";
            if (!await HasHostNameCollisionAsync(dbContext, tenantId, deviceId, candidate, cancellationToken))
            {
                return candidate;
            }
        }

        return null;
    }

    internal static string JoinHostNameParts(string prefix, string middle, string postfix)
    {
        var builder = new StringBuilder();
        var normalizedPrefix = prefix.Trim().TrimEnd('-');
        var normalizedMiddle = middle.Trim();
        var normalizedPostfix = postfix.Trim().TrimStart('-');

        if (!string.IsNullOrEmpty(normalizedPrefix))
        {
            builder.Append(normalizedPrefix);
        }

        if (!string.IsNullOrEmpty(normalizedMiddle))
        {
            if (builder.Length > 0)
            {
                builder.Append('-');
            }

            builder.Append(normalizedMiddle);
        }

        if (!string.IsNullOrEmpty(normalizedPostfix))
        {
            if (builder.Length > 0)
            {
                builder.Append('-');
            }

            builder.Append(normalizedPostfix);
        }

        return builder.ToString();
    }

    internal static string ExtractMacSegment(string macAddress, int noOfChar)
    {
        var normalized = new string(macAddress.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        return normalized.Length <= noOfChar
            ? normalized
            : normalized[^noOfChar..];
    }

    internal static string ExtractSegment(string? source, int noOfChar)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var normalized = new string(source.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        return normalized.Length <= noOfChar
            ? normalized
            : normalized[^noOfChar..];
    }

    internal static string? TryParseSerialFromHardwareJson(string? hardwareJson)
    {
        if (string.IsNullOrWhiteSpace(hardwareJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(hardwareJson);
            var root = document.RootElement;
            foreach (var propertyName in new[] { "serialNumber", "SerialNumber", "serial", "Serial" })
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value.Trim();
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    internal static string TruncateNetBios(string hostName) =>
        hostName.Length <= NetBiosMaxLength
            ? hostName
            : hostName[..NetBiosMaxLength];

    private static async Task<bool> HasHostNameCollisionAsync(
        IntellinodeDbContext dbContext,
        Guid tenantId,
        Guid deviceId,
        string hostName,
        CancellationToken cancellationToken)
    {
        var deviceCollision = await dbContext.Devices
            .AnyAsync(
                d => d.TenantId == tenantId &&
                     d.Id != deviceId &&
                     d.HostName == hostName,
                cancellationToken);

        if (deviceCollision)
        {
            return true;
        }

        return await dbContext.DeviceWindowsComputerNameSettings
            .AnyAsync(
                s => s.DeviceId != deviceId &&
                     s.HostName == hostName &&
                     dbContext.Devices.Any(d => d.Id == s.DeviceId && d.TenantId == tenantId),
                cancellationToken);
    }
}
