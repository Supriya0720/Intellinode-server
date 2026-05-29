using Intellinode.Domain.Enums;

namespace Intellinode.Infrastructure.Services;

public static class DeviceEnrollmentGuard
{
    public static bool IsManaged(EnrollmentState state) =>
        state is EnrollmentState.Active or EnrollmentState.Unlicensed;

    public static (bool Blocked, string ErrorCode, string Message) GetAgentAccessBlock(EnrollmentState state) =>
        state switch
        {
            EnrollmentState.PendingApproval => (true, "DevicePendingApproval", "Device is awaiting administrator approval."),
            EnrollmentState.Rejected => (true, "DeviceRejected", "Device discovery was rejected by an administrator."),
            _ => (false, "", "")
        };

    /// <summary>
    /// Returns SDFT, exists, NOK, or null when heartbeat should continue to task logic (0/1).
    /// </summary>
    public static string? ResolveEnrollmentHeartbeatFlag(EnrollmentState state, bool hasInventory) =>
        state switch
        {
            EnrollmentState.PendingInventory => "SDFT",
            EnrollmentState.PendingApproval when hasInventory => "exists",
            EnrollmentState.PendingApproval => "SDFT",
            EnrollmentState.Active => null,
            EnrollmentState.Unlicensed => null,
            EnrollmentState.Rejected => "NOK",
            EnrollmentState.Disabled => "NOK",
            _ => "SDFT"
        };
}
