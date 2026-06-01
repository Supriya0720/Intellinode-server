using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services.DeviceManager;

public sealed class DeviceManagerRootsService : IDeviceManagerRootsService
{
    private const string ServiceSource = nameof(DeviceManagerRootsService);

    private readonly IntellinodeDbContext _dbContext;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<DeviceManagerRootsService> _logger;

    public DeviceManagerRootsService(
        IntellinodeDbContext dbContext,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<DeviceManagerRootsService> logger)
    {
        _dbContext = dbContext;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
    }

    public async Task<DeviceManagerRootsResponse> GetRootsAsync(
        DeviceManagerRootsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var tenantId = TenantDefaults.DefaultTenantId;
            var groups = await LoadGroupsAsync(tenantId, cancellationToken);
            var devices = await LoadDevicesAsync(tenantId, cancellationToken);

            var statusFilter = DeviceManagerDeviceQueryHelper.NormalizeStatusFilter(query.Status);
            var searchTerm = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
            var hideEmptyWhenFiltered = statusFilter is not null;

            var statusFilteredDevices = DeviceManagerDeviceQueryHelper.FilterDevicesByStatus(devices, statusFilter);
            var childrenByParentId = DeviceManagerGroupHierarchyHelper.BuildChildrenByParentId(groups);

            var rootGroups = groups
                .Where(g => g.ParentGroupId is null)
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var items = new List<DeviceManagerGroupSummaryDto>();
            foreach (var root in rootGroups)
            {
                var summary = DeviceManagerGroupSummaryBuilder.BuildSummary(
                    root,
                    parentId: null,
                    depth: 0,
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

            if (query.IncludeUnassigned)
            {
                var unassignedDevices = statusFilteredDevices.Where(d => d.GroupId is null).ToList();
                if (unassignedDevices.Count > 0)
                {
                    var unassignedSummary = DeviceManagerGroupSummaryBuilder.BuildUnassignedSummary(
                        unassignedDevices,
                        searchTerm);

                    if (unassignedSummary.DeviceCount > 0)
                    {
                        items.Add(unassignedSummary);
                    }
                }
            }

            return new DeviceManagerRootsResponse
            {
                Items = items,
                TotalDeviceCount = devices.Count,
                FilteredDeviceCount = statusFilteredDevices.Count
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
                $"{ServiceSource}.{nameof(GetRootsAsync)}",
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
