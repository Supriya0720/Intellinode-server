using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services.DeviceManager;

public sealed class DeviceManagerGroupChildrenService : IDeviceManagerGroupChildrenService
{
    private const string ServiceSource = nameof(DeviceManagerGroupChildrenService);

    private readonly IntellinodeDbContext _dbContext;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<DeviceManagerGroupChildrenService> _logger;

    public DeviceManagerGroupChildrenService(
        IntellinodeDbContext dbContext,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<DeviceManagerGroupChildrenService> logger)
    {
        _dbContext = dbContext;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
    }

    public async Task<DeviceManagerChildGroupsResponse?> GetChildGroupsAsync(
        Guid parentGroupId,
        DeviceManagerGroupChildrenQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var tenantId = TenantDefaults.DefaultTenantId;
            var parentGroup = await _dbContext.DeviceGroups
                .AsNoTracking()
                .Where(g => g.Id == parentGroupId && g.TenantId == tenantId)
                .Select(g => new { g.Id, g.Name })
                .FirstOrDefaultAsync(cancellationToken);

            if (parentGroup is null)
            {
                return null;
            }

            var groups = await LoadGroupsAsync(tenantId, cancellationToken);
            var devices = await LoadDevicesAsync(tenantId, cancellationToken);
            var groupById = groups.ToDictionary(g => g.Id);
            var parentDepth = DeviceManagerGroupHierarchyHelper.ResolveDepth(parentGroupId, groupById);

            var statusFilter = DeviceManagerDeviceQueryHelper.NormalizeStatusFilter(query.Status);
            var searchTerm = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            var hideEmptyWhenFiltered = statusFilter is not null;

            var statusFilteredDevices = DeviceManagerDeviceQueryHelper.FilterDevicesByStatus(devices, statusFilter);
            var childrenByParentId = DeviceManagerGroupHierarchyHelper.BuildChildrenByParentId(groups);

            var directChildren = childrenByParentId.TryGetValue(parentGroupId, out var children)
                ? children
                : [];

            var items = new List<DeviceManagerGroupSummaryDto>();
            foreach (var child in directChildren)
            {
                var summary = DeviceManagerGroupSummaryBuilder.BuildSummary(
                    child,
                    parentId: parentGroupId,
                    depth: parentDepth + 1,
                    groups,
                    childrenByParentId,
                    statusFilteredDevices,
                    searchTerm,
                    hideEmptyWhenFiltered);

                if (summary is not null)
                {
                    items.Add(summary);
                }
            }

            return new DeviceManagerChildGroupsResponse
            {
                ParentGroupId = parentGroup.Id,
                ParentGroupName = parentGroup.Name,
                ParentDepth = parentDepth,
                Items = items
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await ExceptionLogHelper.SafeLogAsync(
                _exceptionLogWriter,
                _logger,
                $"{ServiceSource}.{nameof(GetChildGroupsAsync)}",
                ex,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    private Task<List<DeviceManagerGroupRow>> LoadGroupsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _dbContext.DeviceGroups
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId)
            .Select(g => new DeviceManagerGroupRow(g.Id, g.ParentGroupId, g.Name, g.SortOrder))
            .ToListAsync(cancellationToken);

    private Task<List<DeviceManagerDeviceRow>> LoadDevicesAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _dbContext.Devices
            .AsNoTracking()
            .Include(d => d.Inventory)
            .Where(d => d.TenantId == tenantId)
            .Select(d => new DeviceManagerDeviceRow(
                d.Id,
                d.GroupId,
                d.HostName,
                d.MacAddress,
                d.Os,
                d.IsOnline,
                d.ClientStatus,
                d.EnrollmentState,
                d.LastHeartbeatUtc,
                d.Inventory != null ? d.Inventory.HardwareJson : null))
            .ToListAsync(cancellationToken);
}
