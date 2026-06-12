namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows power plan settings per device (FusionX Power Management Settings module).
/// Inner <see cref="SettingsJson"/> uses FusionX <c>XPPowerManagement</c> field names without the WinCELinux wrapper.
/// See ADR-0004: full agent payload is hydrated at poll time from <see cref="SettingsJson"/>.
/// </summary>
public sealed class DeviceWindowsPowerManagementSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    public string ActivePlanName { get; set; } = "Balanced";
    public int AgentAction { get; set; }

    /// <summary>
    /// Full FusionX <c>XPPowerManagement</c> inner document (no WinCELinux wrapper; TaskID/AgentAction merged at hydration).
    /// </summary>
    public string SettingsJson { get; set; } = "{}";

    public long SettingsVersion { get; set; } = 1;
    public bool PendingApply { get; set; }
    public long? LastAppliedVersion { get; set; }
    public DateTime? LastAppliedUtc { get; set; }
    public string? LastApplyStatus { get; set; }
    public string? LastApplyMessage { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
