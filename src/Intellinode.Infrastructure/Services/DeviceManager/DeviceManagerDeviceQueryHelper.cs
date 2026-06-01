using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services.DeviceManager;

internal static class DeviceManagerDeviceQueryHelper
{
    public const int MaxPageSize = 200;

    public static string? NormalizeStatusFilter(string status)
    {
        if (string.IsNullOrWhiteSpace(status) ||
            status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return status.Trim();
    }

    public static (int Page, int PageSize) NormalizePagination(int page, int pageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        return (normalizedPage, normalizedPageSize);
    }

    public static int CalculateTotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

    public static IQueryable<Device> ApplyTenantScope(IQueryable<Device> query, Guid tenantId) =>
        query.AsNoTracking().Where(d => d.TenantId == tenantId);

    public static IQueryable<Device> ApplyGroupScope(IQueryable<Device> query, Guid? groupId)
    {
        if (groupId.HasValue)
        {
            return query.Where(d => d.GroupId == groupId.Value);
        }

        return query.Where(d => d.GroupId == null);
    }

    public static IQueryable<Device> ApplySearchFilter(IQueryable<Device> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var term = $"%{search.Trim()}%";
        return query.Where(d =>
            EF.Functions.ILike(d.HostName, term) ||
            EF.Functions.ILike(d.MacAddress, term));
    }

    public static IQueryable<Device> ApplyStatusFilter(IQueryable<Device> query, string? statusFilter)
    {
        if (statusFilter is null)
        {
            return query;
        }

        var staleThreshold = DateTime.UtcNow.Subtract(DeviceManagerStatusHelper.StaleHeartbeatThreshold);

        if (statusFilter.Equals("Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(d => d.EnrollmentState == EnrollmentState.Disabled);
        }

        if (statusFilter.Equals("Online", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(d =>
                d.EnrollmentState != EnrollmentState.Disabled &&
                d.IsOnline &&
                (d.ClientStatus.Trim().ToUpper() == ClientPowerStatus.On ||
                 d.ClientStatus.Trim().ToUpper().StartsWith(ClientPowerStatus.On + "~")));
        }

        if (statusFilter.Equals("Stale", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(d =>
                d.EnrollmentState != EnrollmentState.Disabled &&
                !(d.IsOnline &&
                  (d.ClientStatus.Trim().ToUpper() == ClientPowerStatus.On ||
                   d.ClientStatus.Trim().ToUpper().StartsWith(ClientPowerStatus.On + "~"))) &&
                d.LastHeartbeatUtc != null &&
                d.LastHeartbeatUtc < staleThreshold);
        }

        if (statusFilter.Equals("Offline", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(d =>
                d.EnrollmentState != EnrollmentState.Disabled &&
                !(d.IsOnline &&
                  (d.ClientStatus.Trim().ToUpper() == ClientPowerStatus.On ||
                   d.ClientStatus.Trim().ToUpper().StartsWith(ClientPowerStatus.On + "~"))) &&
                (d.LastHeartbeatUtc == null || d.LastHeartbeatUtc >= staleThreshold));
        }

        return query;
    }

    public static IQueryable<Device> ApplySort(IQueryable<Device> query, string sortBy, bool descending)
    {
        return sortBy.Trim().ToLowerInvariant() switch
        {
            "macaddress" => descending
                ? query.OrderByDescending(d => d.MacAddress)
                : query.OrderBy(d => d.MacAddress),
            "status" => descending
                ? query.OrderByDescending(d => d.EnrollmentState)
                    .ThenByDescending(d => d.IsOnline)
                    .ThenByDescending(d => d.ClientStatus)
                : query.OrderBy(d => d.EnrollmentState)
                    .ThenBy(d => d.IsOnline)
                    .ThenBy(d => d.ClientStatus),
            "lastheartbeatutc" => descending
                ? query.OrderByDescending(d => d.LastHeartbeatUtc)
                : query.OrderBy(d => d.LastHeartbeatUtc),
            _ => descending
                ? query.OrderByDescending(d => d.HostName)
                : query.OrderBy(d => d.HostName)
        };
    }

    public static DeviceManagerDeviceRowDto MapToRowDto(Device device)
    {
        var status = DeviceManagerStatusHelper.MapDeviceStatus(
            device.EnrollmentState,
            device.IsOnline,
            device.ClientStatus,
            device.LastHeartbeatUtc);

        return new DeviceManagerDeviceRowDto
        {
            Id = device.Id,
            HostName = device.HostName,
            MacAddress = device.MacAddress,
            Status = status,
            BatteryPercent = DeviceManagerStatusHelper.TryParseBatteryPercent(device.Inventory?.HardwareJson),
            AgentType = DeviceManagerStatusHelper.MapAgentType(device.Os),
            OsPlatform = device.Os,
            IsOnline = DeviceManagerStatusHelper.IsDeviceOnline(device.IsOnline, device.ClientStatus),
            LastHeartbeatUtc = device.LastHeartbeatUtc,
            EnrollmentState = device.EnrollmentState,
            GroupId = device.GroupId
        };
    }

    public static List<DeviceManagerDeviceRow> FilterDevicesByStatus(
        IEnumerable<DeviceManagerDeviceRow> devices,
        string? statusFilter)
    {
        if (statusFilter is null)
        {
            return devices.ToList();
        }

        return devices
            .Where(d => DeviceManagerStatusHelper.DeviceStatusMatchesFilter(
                DeviceManagerAggregateCalculator.MapDeviceStatus(d),
                statusFilter))
            .ToList();
    }

    public static bool GroupNameMatchesSearch(string name, string searchTerm) =>
        name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

    public static bool DeviceMatchesSearch(DeviceManagerDeviceRow device, string searchTerm) =>
        device.HostName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
        device.MacAddress.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
}
