namespace Intellinode.Infrastructure.Services.DeviceManager;

internal static class DeviceManagerAggregateCalculator
{
    public sealed record DeviceStatusCounts(
        int DeviceCount,
        int OnlineCount,
        int OfflineCount,
        int MaintenanceCount);

    public static DeviceStatusCounts ComputeFromDevices(IEnumerable<DeviceManagerDeviceRow> devices)
    {
        var deviceCount = 0;
        var onlineCount = 0;
        var offlineCount = 0;
        var maintenanceCount = 0;

        foreach (var device in devices)
        {
            deviceCount++;
            switch (MapDeviceStatus(device))
            {
                case "Online":
                    onlineCount++;
                    break;
                case "Maintenance":
                    maintenanceCount++;
                    break;
                default:
                    offlineCount++;
                    break;
            }
        }

        return new DeviceStatusCounts(deviceCount, onlineCount, offlineCount, maintenanceCount);
    }

    public static string MapDeviceStatus(DeviceManagerDeviceRow device) =>
        DeviceManagerStatusHelper.MapDeviceStatus(
            device.EnrollmentState,
            device.IsOnline,
            device.ClientStatus,
            device.LastHeartbeatUtc);
}
