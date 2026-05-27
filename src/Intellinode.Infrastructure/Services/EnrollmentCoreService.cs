using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class EnrollmentCoreService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public EnrollmentCoreService(IntellinodeDbContext dbContext, ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<AgentEnrollmentToken?> FindValidEnrollmentTokenAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashToken(token.Trim());
        var enrollment = await _dbContext.AgentEnrollmentTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash && t.ConsumedUtc == null,
                cancellationToken);

        if (enrollment is null || enrollment.ExpiresUtc < DateTime.UtcNow)
        {
            return null;
        }

        return enrollment;
    }

    public static AgentEnrollResult? ValidatePlatform(AgentEnrollmentToken enrollment, AgentPlatform expected)
    {
        if (enrollment.Platform == expected)
        {
            return null;
        }

        return AgentEnrollResult.Failure(
            "PlatformMismatch",
            $"This enrollment token is not valid for {expected} agents.");
    }

    public async Task<(Device Device, bool IsNew)> UpsertDeviceForEnrollmentAsync(
        string macAddress,
        string os,
        CancellationToken cancellationToken)
    {
        var device = await _dbContext.Devices.FirstOrDefaultAsync(
            d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == macAddress,
            cancellationToken);
        if (device is null)
        {
            var defaultGroup = await _dbContext.DeviceGroups.FirstOrDefaultAsync(
                g => g.TenantId == TenantDefaults.DefaultTenantId && g.IsDefault,
                cancellationToken);
            device = new Device
            {
                TenantId = TenantDefaults.DefaultTenantId,
                MacAddress = macAddress,
                IsRegistered = true,
                Os = os,
                EnrollmentState = EnrollmentState.PendingInventory,
                GroupId = defaultGroup?.Id
            };
            _dbContext.Devices.Add(device);
            return (device, true);
        }

        device.IsRegistered = true;
        device.Os = os;
        device.UpdatedUtc = DateTime.UtcNow;
        return (device, false);
    }

    public static void CompleteEnrollment(AgentEnrollmentToken enrollment, Device device)
    {
        enrollment.ConsumedUtc = DateTime.UtcNow;
        enrollment.DeviceId = device.Id;
    }

    public static (string? MacAddress, string? ErrorCode) ResolveMacAddress(
        string? deviceIdentity,
        AgentEnrollmentToken enrollment)
    {
        var requestedMac = deviceIdentity?.Trim();
        if (!string.IsNullOrWhiteSpace(enrollment.MacAddress))
        {
            if (!string.IsNullOrWhiteSpace(requestedMac) &&
                !string.Equals(requestedMac, enrollment.MacAddress, StringComparison.OrdinalIgnoreCase))
            {
                return (null, "MacMismatch");
            }

            return (enrollment.MacAddress, null);
        }

        return string.IsNullOrWhiteSpace(requestedMac) ? (null, "InvalidEnrollmentToken") : (requestedMac, null);
    }

    public async Task<bool> DeviceHasInventoryAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        if (_dbContext.DeviceInventories.Local.Any(i => i.DeviceId == deviceId))
        {
            return true;
        }

        return await _dbContext.DeviceInventories.AnyAsync(i => i.DeviceId == deviceId, cancellationToken);
    }
}
