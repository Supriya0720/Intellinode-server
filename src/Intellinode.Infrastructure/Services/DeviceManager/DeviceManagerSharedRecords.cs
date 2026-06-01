namespace Intellinode.Infrastructure.Services.DeviceManager;

internal static class DeviceManagerConstants
{
    public static readonly Guid UnassignedNodeId = Guid.Empty;
}

internal sealed record DeviceManagerGroupRow(Guid Id, Guid? ParentGroupId, string Name, int SortOrder);

internal sealed record DeviceManagerDeviceRow(
    Guid Id,
    Guid? GroupId,
    string HostName,
    string MacAddress,
    string Os,
    bool IsOnline,
    string ClientStatus,
    Domain.Enums.EnrollmentState EnrollmentState,
    DateTime? LastHeartbeatUtc,
    string? HardwareJson);
