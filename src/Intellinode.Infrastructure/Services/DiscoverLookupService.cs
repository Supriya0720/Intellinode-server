using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class DiscoverLookupService : IDiscoverLookupService
{
    private const int MaxPageSize = 200;

    private readonly IntellinodeDbContext _dbContext;
    private readonly IAgentCommunicationLogWriter _communicationLogWriter;

    public DiscoverLookupService(
        IntellinodeDbContext dbContext,
        IAgentCommunicationLogWriter communicationLogWriter)
    {
        _dbContext = dbContext;
        _communicationLogWriter = communicationLogWriter;
    }

    public async Task<PagedDiscoverLookupResponse> ListAsync(
        DiscoverLookupQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var lookups = _dbContext.DiscoverLookups
            .AsNoTracking()
            .Include(d => d.Device)
            .ThenInclude(device => device!.Group)
            .Where(d => d.TenantId == TenantDefaults.DefaultTenantId);

        if (!string.IsNullOrWhiteSpace(query.Status) &&
            !query.Status.Equals("All", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<DiscoverLookupStatus>(query.Status, true, out var status))
        {
            lookups = lookups.Where(d => d.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            lookups = lookups.Where(d =>
                EF.Functions.ILike(d.MacAddress, term) ||
                EF.Functions.ILike(d.HostName, term) ||
                EF.Functions.ILike(d.IpAddress, term));
        }

        var totalCount = await lookups.CountAsync(cancellationToken);
        var descending = query.SortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        lookups = ApplySort(lookups, query.SortBy, descending);

        var items = await lookups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DiscoverLookupListItemDto
            {
                Id = d.Id,
                MacAddress = d.MacAddress,
                HostName = d.HostName,
                IpAddress = d.IpAddress,
                OsName = d.OsName,
                OsVersion = d.OsVersion,
                AgentVersion = d.AgentVersion,
                Status = d.Status,
                DiscoveredUtc = d.DiscoveredUtc,
                UpdatedUtc = d.UpdatedUtc,
                DeviceId = d.DeviceId,
                DeviceEnrollmentState = d.Device != null ? d.Device.EnrollmentState : null,
                LastHeartbeatUtc = d.Device != null ? d.Device.LastHeartbeatUtc : null,
                GroupName = d.Device != null && d.Device.Group != null ? d.Device.Group.Name : null
            })
            .ToListAsync(cancellationToken);

        return new PagedDiscoverLookupResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<DiscoverLookupDetailDto?> GetByMacAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        var lookup = await FindLookupWithDetailsAsync(macAddress, cancellationToken);
        if (lookup is null)
        {
            return null;
        }

        return MapDetail(lookup);
    }

    public async Task<DiscoverLookupStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var todayStart = DateTime.UtcNow.Date;

        var pendingCount = await _dbContext.DiscoverLookups
            .AsNoTracking()
            .CountAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.Status == DiscoverLookupStatus.Pending,
                cancellationToken);

        var approvedTodayCount = await _dbContext.DiscoverLookups
            .AsNoTracking()
            .CountAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId &&
                     d.ApprovedUtc != null &&
                     d.ApprovedUtc >= todayStart,
                cancellationToken);

        var rejectedTodayCount = await _dbContext.DiscoverLookups
            .AsNoTracking()
            .CountAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId &&
                     d.RejectedUtc != null &&
                     d.RejectedUtc >= todayStart,
                cancellationToken);

        return new DiscoverLookupStatsResponse
        {
            PendingCount = pendingCount,
            ApprovedTodayCount = approvedTodayCount,
            RejectedTodayCount = rejectedTodayCount
        };
    }

    public async Task<DiscoverLookupOperationResult<ApproveDiscoveryResponse>> ApproveAsync(
        string macAddress,
        Guid adminId,
        ApproveDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var lookup = await FindLookupTrackedAsync(macAddress, cancellationToken);
        if (lookup is null)
        {
            return DiscoverLookupOperationResult<ApproveDiscoveryResponse>.Failure(
                "DiscoveryNotFound",
                $"No discovery entry found for MAC address '{macAddress.Trim()}'.");
        }

        if (lookup.Status != DiscoverLookupStatus.Pending)
        {
            return DiscoverLookupOperationResult<ApproveDiscoveryResponse>.Failure(
                "DiscoveryAlreadyProcessed",
                $"Discovery entry for MAC '{lookup.MacAddress}' is already {lookup.Status}.");
        }

        if (lookup.DeviceId is null)
        {
            return DiscoverLookupOperationResult<ApproveDiscoveryResponse>.Failure(
                "InventoryMissing",
                "Discovery entry is not linked to a device.");
        }

        var device = await _dbContext.Devices
            .Include(d => d.Group)
            .FirstOrDefaultAsync(d => d.Id == lookup.DeviceId.Value, cancellationToken);

        if (device is null)
        {
            return DiscoverLookupOperationResult<ApproveDiscoveryResponse>.Failure(
                "DiscoveryNotFound",
                "Linked device was not found.");
        }

        var hasInventory = await _dbContext.DeviceInventories
            .AnyAsync(i => i.DeviceId == device.Id, cancellationToken);

        if (!hasInventory)
        {
            return DiscoverLookupOperationResult<ApproveDiscoveryResponse>.Failure(
                "InventoryMissing",
                "Device inventory must be uploaded before approval.");
        }

        var group = await ResolveGroupAsync(request.GroupId, cancellationToken);
        if (group is null)
        {
            return DiscoverLookupOperationResult<ApproveDiscoveryResponse>.Failure(
                "GroupNotFound",
                request.GroupId.HasValue
                    ? $"Device group '{request.GroupId}' was not found."
                    : "Default device group was not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.HostName))
        {
            device.HostName = request.HostName.Trim();
        }

        device.IsRegistered = true;
        device.EnrollmentState = EnrollmentState.Active;
        device.GroupId = group.Id;
        device.UpdatedUtc = DateTime.UtcNow;

        var approvedUtc = DateTime.UtcNow;
        lookup.Status = DiscoverLookupStatus.Approved;
        lookup.ApprovedByAdminId = adminId;
        lookup.ApprovedUtc = approvedUtc;
        lookup.Notes = request.Notes?.Trim();
        lookup.UpdatedUtc = approvedUtc;

        await _communicationLogWriter.LogAsync(
            device.Id,
            lookup.MacAddress,
            "Inbound",
            $"/api/v1/admin/discover/{lookup.MacAddress}/approve",
            "Approved",
            request.Notes,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return DiscoverLookupOperationResult<ApproveDiscoveryResponse>.Success(new ApproveDiscoveryResponse
        {
            MacAddress = lookup.MacAddress,
            DeviceId = device.Id,
            EnrollmentState = device.EnrollmentState,
            GroupId = group.Id,
            GroupName = group.Name,
            ApprovedUtc = approvedUtc
        });
    }

    public async Task<DiscoverLookupOperationResult<bool>> RejectAsync(
        string macAddress,
        Guid adminId,
        RejectDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var lookup = await FindLookupTrackedAsync(macAddress, cancellationToken);
        if (lookup is null)
        {
            return DiscoverLookupOperationResult<bool>.Failure(
                "DiscoveryNotFound",
                $"No discovery entry found for MAC address '{macAddress.Trim()}'.");
        }

        if (lookup.Status != DiscoverLookupStatus.Pending)
        {
            return DiscoverLookupOperationResult<bool>.Failure(
                "DiscoveryAlreadyProcessed",
                $"Discovery entry for MAC '{lookup.MacAddress}' is already {lookup.Status}.");
        }

        var rejectedUtc = DateTime.UtcNow;
        lookup.Status = DiscoverLookupStatus.Rejected;
        lookup.RejectedByAdminId = adminId;
        lookup.RejectedUtc = rejectedUtc;
        lookup.RejectionReason = request.Reason.Trim();
        lookup.UpdatedUtc = rejectedUtc;

        if (lookup.DeviceId is Guid deviceId)
        {
            var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);
            if (device is not null)
            {
                device.EnrollmentState = EnrollmentState.Rejected;
                device.IsRegistered = false;
                device.UpdatedUtc = rejectedUtc;
            }

            var tokens = await _dbContext.AgentRefreshTokens
                .Where(t => t.DeviceId == deviceId && t.RevokedUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.RevokedUtc = rejectedUtc;
            }
        }

        await _communicationLogWriter.LogAsync(
            lookup.DeviceId,
            lookup.MacAddress,
            "Inbound",
            $"/api/v1/admin/discover/{lookup.MacAddress}/reject",
            "Rejected",
            request.Reason.Trim(),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return DiscoverLookupOperationResult<bool>.Success(true);
    }

    public async Task<DiscoverLookupOperationResult<bool>> DismissAsync(
        string macAddress,
        Guid adminId,
        DismissDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var lookup = await FindLookupTrackedAsync(macAddress, cancellationToken);
        if (lookup is null)
        {
            return DiscoverLookupOperationResult<bool>.Failure(
                "DiscoveryNotFound",
                $"No discovery entry found for MAC address '{macAddress.Trim()}'.");
        }

        if (lookup.Status == DiscoverLookupStatus.Approved)
        {
            return DiscoverLookupOperationResult<bool>.Failure(
                "DiscoveryAlreadyProcessed",
                "Approved discovery entries cannot be dismissed.");
        }

        if (lookup.Status is not (DiscoverLookupStatus.Pending or DiscoverLookupStatus.Rejected))
        {
            return DiscoverLookupOperationResult<bool>.Failure(
                "DiscoveryAlreadyProcessed",
                $"Discovery entry for MAC '{lookup.MacAddress}' cannot be dismissed.");
        }

        var dismissedUtc = DateTime.UtcNow;
        var wasPending = lookup.Status == DiscoverLookupStatus.Pending;
        var deviceId = lookup.DeviceId;
        var lookupMac = lookup.MacAddress;

        _dbContext.DiscoverLookups.Remove(lookup);

        if (wasPending && deviceId is Guid pendingDeviceId)
        {
            var device = await _dbContext.Devices.FirstOrDefaultAsync(d => d.Id == pendingDeviceId, cancellationToken);
            if (device is not null)
            {
                device.EnrollmentState = EnrollmentState.Disabled;
                device.IsRegistered = false;
                device.UpdatedUtc = dismissedUtc;
            }

            var tokens = await _dbContext.AgentRefreshTokens
                .Where(t => t.DeviceId == pendingDeviceId && t.RevokedUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.RevokedUtc = dismissedUtc;
            }
        }

        await _communicationLogWriter.LogAsync(
            deviceId,
            lookupMac,
            "Inbound",
            $"/api/v1/admin/discover/{lookupMac}",
            "Dismissed",
            request.Reason?.Trim(),
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return DiscoverLookupOperationResult<bool>.Success(true);
    }

    public async Task<BulkApproveDiscoveryResponse> BulkApproveAsync(
        Guid adminId,
        BulkApproveDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BulkApproveDiscoveryItemResult>();

        foreach (var mac in request.MacAddresses)
        {
            var approveResult = await ApproveAsync(
                mac,
                adminId,
                new ApproveDiscoveryRequest(),
                cancellationToken);

            if (approveResult.IsSuccess)
            {
                results.Add(new BulkApproveDiscoveryItemResult
                {
                    MacAddress = mac.Trim(),
                    Succeeded = true
                });
            }
            else
            {
                results.Add(new BulkApproveDiscoveryItemResult
                {
                    MacAddress = mac.Trim(),
                    Succeeded = false,
                    ErrorCode = approveResult.ErrorCode,
                    Message = approveResult.Message
                });
            }
        }

        return new BulkApproveDiscoveryResponse
        {
            Results = results,
            SucceededCount = results.Count(r => r.Succeeded),
            FailedCount = results.Count(r => !r.Succeeded)
        };
    }

    private static IQueryable<DiscoverLookup> ApplySort(
        IQueryable<DiscoverLookup> query,
        string sortBy,
        bool descending)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "macaddress" => descending
                ? query.OrderByDescending(d => d.MacAddress)
                : query.OrderBy(d => d.MacAddress),
            "hostname" => descending
                ? query.OrderByDescending(d => d.HostName)
                : query.OrderBy(d => d.HostName),
            "status" => descending
                ? query.OrderByDescending(d => d.Status)
                : query.OrderBy(d => d.Status),
            "updatedutc" => descending
                ? query.OrderByDescending(d => d.UpdatedUtc)
                : query.OrderBy(d => d.UpdatedUtc),
            _ => descending
                ? query.OrderByDescending(d => d.DiscoveredUtc)
                : query.OrderBy(d => d.DiscoveredUtc)
        };
    }

    private async Task<DiscoverLookup?> FindLookupTrackedAsync(
        string macAddress,
        CancellationToken cancellationToken)
    {
        var normalizedMac = macAddress.Trim();
        return await _dbContext.DiscoverLookups
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId &&
                     EF.Functions.ILike(d.MacAddress, normalizedMac),
                cancellationToken);
    }

    private async Task<DiscoverLookup?> FindLookupWithDetailsAsync(
        string macAddress,
        CancellationToken cancellationToken)
    {
        var normalizedMac = macAddress.Trim();
        return await _dbContext.DiscoverLookups
            .AsNoTracking()
            .Include(d => d.Device)
            .ThenInclude(device => device!.Group)
            .Include(d => d.Device)
            .ThenInclude(device => device!.Inventory)
            .Include(d => d.ApprovedByAdmin)
            .Include(d => d.RejectedByAdmin)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId &&
                     EF.Functions.ILike(d.MacAddress, normalizedMac),
                cancellationToken);
    }

    private async Task<DeviceGroup?> ResolveGroupAsync(
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        if (groupId is Guid requestedGroupId)
        {
            return await _dbContext.DeviceGroups.FirstOrDefaultAsync(
                g => g.TenantId == TenantDefaults.DefaultTenantId && g.Id == requestedGroupId,
                cancellationToken);
        }

        return await _dbContext.DeviceGroups.FirstOrDefaultAsync(
            g => g.TenantId == TenantDefaults.DefaultTenantId && g.IsDefault,
            cancellationToken);
    }

    private static DiscoverLookupDetailDto MapDetail(DiscoverLookup lookup)
    {
        var detail = new DiscoverLookupDetailDto
        {
            Id = lookup.Id,
            MacAddress = lookup.MacAddress,
            HostName = lookup.HostName,
            IpAddress = lookup.IpAddress,
            Domain = lookup.Domain,
            OsName = lookup.OsName,
            OsVersion = lookup.OsVersion,
            AgentVersion = lookup.AgentVersion,
            DiscoveryType = lookup.DiscoveryType,
            Status = lookup.Status,
            DiscoveredUtc = lookup.DiscoveredUtc,
            UpdatedUtc = lookup.UpdatedUtc,
            DeviceId = lookup.DeviceId,
            DeviceEnrollmentState = lookup.Device?.EnrollmentState,
            LastHeartbeatUtc = lookup.Device?.LastHeartbeatUtc,
            GroupId = lookup.Device?.GroupId,
            GroupName = lookup.Device?.Group?.Name
        };

        if (lookup.Device?.Inventory is DeviceInventory inventory)
        {
            detail.Inventory = new DiscoverLookupInventoryDto
            {
                Hardware = ParseJsonElement(inventory.HardwareJson),
                Network = ParseJsonElement(inventory.NetworkJson),
                OsInfo = ParseJsonElement(inventory.OsInfoJson),
                Security = ParseJsonElement(inventory.SecurityJson),
                CollectedUtc = inventory.CollectedUtc,
                Version = inventory.Version
            };
        }

        if (lookup.Status == DiscoverLookupStatus.Approved &&
            lookup.ApprovedByAdminId is Guid approvedBy &&
            lookup.ApprovedUtc is DateTime approvedUtc)
        {
            detail.Approval = new DiscoverLookupApprovalDto
            {
                AdminId = approvedBy,
                AdminDisplayName = lookup.ApprovedByAdmin?.DisplayName ?? string.Empty,
                ApprovedUtc = approvedUtc,
                Notes = lookup.Notes
            };
        }

        if (lookup.Status == DiscoverLookupStatus.Rejected &&
            lookup.RejectedByAdminId is Guid rejectedBy &&
            lookup.RejectedUtc is DateTime rejectedUtc)
        {
            detail.Rejection = new DiscoverLookupRejectionDto
            {
                AdminId = rejectedBy,
                AdminDisplayName = lookup.RejectedByAdmin?.DisplayName ?? string.Empty,
                RejectedUtc = rejectedUtc,
                Reason = lookup.RejectionReason
            };
        }

        return detail;
    }

    private static JsonElement? ParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
