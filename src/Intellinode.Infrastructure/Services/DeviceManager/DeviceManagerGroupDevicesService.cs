using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services.DeviceManager;

public sealed class DeviceManagerGroupDevicesService : IDeviceManagerGroupDevicesService
{
    private const string ServiceSource = nameof(DeviceManagerGroupDevicesService);

    private readonly IntellinodeDbContext _dbContext;
    private readonly IExceptionLogWriter _exceptionLogWriter;
    private readonly ILogger<DeviceManagerGroupDevicesService> _logger;

    public DeviceManagerGroupDevicesService(
        IntellinodeDbContext dbContext,
        IExceptionLogWriter exceptionLogWriter,
        ILogger<DeviceManagerGroupDevicesService> logger)
    {
        _dbContext = dbContext;
        _exceptionLogWriter = exceptionLogWriter;
        _logger = logger;
    }

    public async Task<PagedDeviceManagerDevicesResponse?> GetGroupDevicesAsync(
        Guid groupId,
        DeviceManagerGroupDevicesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var tenantId = TenantDefaults.DefaultTenantId;
            var group = await _dbContext.DeviceGroups
                .AsNoTracking()
                .Where(g => g.Id == groupId && g.TenantId == tenantId)
                .Select(g => new { g.Id, g.Name })
                .FirstOrDefaultAsync(cancellationToken);

            if (group is null)
            {
                return null;
            }

            return await QueryPagedDevicesAsync(
                tenantId,
                groupId,
                group.Name,
                query,
                cancellationToken);
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
                $"{ServiceSource}.{nameof(GetGroupDevicesAsync)}",
                ex,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    public async Task<PagedDeviceManagerDevicesResponse> GetUnassignedDevicesAsync(
        DeviceManagerGroupDevicesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var tenantId = TenantDefaults.DefaultTenantId;
            return await QueryPagedDevicesAsync(
                tenantId,
                groupId: null,
                groupName: "Unassigned",
                query,
                cancellationToken);
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
                $"{ServiceSource}.{nameof(GetUnassignedDevicesAsync)}",
                ex,
                cancellationToken: cancellationToken);
            throw;
        }
    }

    private async Task<PagedDeviceManagerDevicesResponse> QueryPagedDevicesAsync(
        Guid tenantId,
        Guid? groupId,
        string? groupName,
        DeviceManagerGroupDevicesQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = DeviceManagerDeviceQueryHelper.NormalizePagination(query.Page, query.PageSize);
        var statusFilter = DeviceManagerDeviceQueryHelper.NormalizeStatusFilter(query.Status);
        var descending = query.SortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        var devicesQuery = DeviceManagerDeviceQueryHelper.ApplyTenantScope(_dbContext.Devices, tenantId);
        devicesQuery = DeviceManagerDeviceQueryHelper.ApplyGroupScope(devicesQuery, groupId);
        devicesQuery = DeviceManagerDeviceQueryHelper.ApplySearchFilter(devicesQuery, query.Search);
        devicesQuery = DeviceManagerDeviceQueryHelper.ApplyStatusFilter(devicesQuery, statusFilter);
        devicesQuery = DeviceManagerDeviceQueryHelper.ApplySort(devicesQuery, query.SortBy, descending);

        var totalCount = await devicesQuery.CountAsync(cancellationToken);

        var devices = await devicesQuery
            .Include(d => d.Inventory)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = devices.Select(DeviceManagerDeviceQueryHelper.MapToRowDto).ToList();

        return new PagedDeviceManagerDevicesResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = DeviceManagerDeviceQueryHelper.CalculateTotalPages(totalCount, pageSize),
            GroupId = groupId ?? DeviceManagerConstants.UnassignedNodeId,
            GroupName = groupName
        };
    }
}
