namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows 802.1X security settings per device.
/// Persisted JSON uses FusionX <c>Windows_802_1x</c> struct field names (inner document only).
/// See ADR-0001: full agent payload is hydrated at poll time from <see cref="SettingsJson"/>.
/// </summary>
public sealed class DeviceWindows8021xSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    /// <summary>
    /// Full FusionX <c>Windows_802_1x</c> document (inner object only, not the WinCELinux wrapper).
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
