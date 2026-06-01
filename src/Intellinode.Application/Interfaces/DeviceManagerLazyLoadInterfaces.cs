using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Interfaces;

public interface IDeviceManagerRootsService
{
    Task<DeviceManagerRootsResponse> GetRootsAsync(
        DeviceManagerRootsQuery query,
        CancellationToken cancellationToken = default);
}

public interface IDeviceManagerGroupChildrenService
{
    Task<DeviceManagerChildGroupsResponse?> GetChildGroupsAsync(
        Guid parentGroupId,
        DeviceManagerGroupChildrenQuery query,
        CancellationToken cancellationToken = default);
}

public interface IDeviceManagerGroupDevicesService
{
    Task<PagedDeviceManagerDevicesResponse?> GetGroupDevicesAsync(
        Guid groupId,
        DeviceManagerGroupDevicesQuery query,
        CancellationToken cancellationToken = default);

    Task<PagedDeviceManagerDevicesResponse> GetUnassignedDevicesAsync(
        DeviceManagerGroupDevicesQuery query,
        CancellationToken cancellationToken = default);
}
