using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class DiscoverLookupWriter : IDiscoverLookupWriter
{
    private readonly IntellinodeDbContext _dbContext;

    public DiscoverLookupWriter(IntellinodeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertPendingFromInventoryAsync(
        Device device,
        AgentInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        InventoryFieldMapper.ApplyToDevice(device, request);

        var lookup = await _dbContext.DiscoverLookups.FirstOrDefaultAsync(
            d => d.TenantId == TenantDefaults.DefaultTenantId &&
                 EF.Functions.ILike(d.MacAddress, device.MacAddress),
            cancellationToken);

        if (DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            if (lookup is null)
            {
                return;
            }

            ApplyLookupFields(lookup, device);
            lookup.UpdatedUtc = DateTime.UtcNow;
            return;
        }

        if (lookup is null)
        {
            lookup = new DiscoverLookup
            {
                TenantId = TenantDefaults.DefaultTenantId,
                DiscoveredUtc = DateTime.UtcNow
            };
            _dbContext.DiscoverLookups.Add(lookup);
        }
        else if (lookup.Status == DiscoverLookupStatus.Rejected)
        {
            lookup.RejectedByAdminId = null;
            lookup.RejectedUtc = null;
            lookup.RejectionReason = null;
            lookup.ApprovedByAdminId = null;
            lookup.ApprovedUtc = null;
            lookup.Notes = null;
        }

        ApplyLookupFields(lookup, device);

        if (lookup.Status != DiscoverLookupStatus.Approved)
        {
            lookup.Status = DiscoverLookupStatus.Pending;
        }

        lookup.UpdatedUtc = DateTime.UtcNow;
    }

    private static void ApplyLookupFields(DiscoverLookup lookup, Device device)
    {
        lookup.DeviceId = device.Id;
        lookup.MacAddress = device.MacAddress;
        lookup.HostName = device.HostName;
        lookup.IpAddress = device.IpAddress;
        lookup.Domain = device.Domain;
        lookup.OsName = device.Os;
        lookup.OsVersion = device.OsVersion;
        lookup.AgentVersion = device.AgentVersion;
        lookup.DiscoveryType = "AgentSelfDiscovery";
    }

    public async Task SyncPendingFromHeartbeatAsync(
        Device device,
        AgentClientStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (device.EnrollmentState != EnrollmentState.PendingApproval)
        {
            return;
        }

        var lookup = await _dbContext.DiscoverLookups.FirstOrDefaultAsync(
            d => d.TenantId == TenantDefaults.DefaultTenantId &&
                 d.Status == DiscoverLookupStatus.Pending &&
                 (d.DeviceId == device.Id ||
                  EF.Functions.ILike(d.MacAddress, device.MacAddress)),
            cancellationToken);

        if (lookup is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.HostName))
        {
            lookup.HostName = request.HostName.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(device.HostName))
        {
            lookup.HostName = device.HostName;
        }

        var ipAddress = request.IpAddress.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            lookup.IpAddress = ipAddress;
        }
        else if (!string.IsNullOrWhiteSpace(device.IpAddress))
        {
            lookup.IpAddress = device.IpAddress;
        }

        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            lookup.Domain = request.Domain.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(device.Domain))
        {
            lookup.Domain = device.Domain;
        }

        lookup.UpdatedUtc = DateTime.UtcNow;
    }
}
