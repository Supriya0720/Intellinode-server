namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows Wireless Properties (WiFi security profile) per device and SSID.
/// Persisted JSON uses FusionX <c>XPWirelessNetworkSecuritySettings</c> struct field names (inner document only).
/// See ADR-0003: full agent payload is hydrated at poll time from <see cref="SettingsJson"/>.
/// </summary>
public sealed class DeviceWindowsWirelessProfileSettings
{
    public long ProfileKey { get; set; }
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;

    /// <summary>FusionX SSID (<c>txtW_networkname</c>, max 128).</summary>
    public string Ssid { get; set; } = string.Empty;

    /// <summary>
    /// Full FusionX <c>XPWirelessNetworkSecuritySettings</c> document (inner object only, not the WinCELinux wrapper).
    /// Field names: <c>strNetworkSSDIName</c>, <c>strNetworkAuthentication</c>, <c>strNetworkDataEncr</c>,
    /// <c>strNetworkKey</c>, <c>strNetworkPPK</c>, <c>iNetworkKeyIndex</c>, <c>strNetworkName</c>, <c>strStatus</c>,
    /// <c>Conn_Auto_WhenIn_Range</c>, <c>Text1</c>, <c>Text2</c>, <c>Text3</c>, <c>TaskID</c>, <c>AgentAction</c>.
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

    public ICollection<DeviceWindowsWirelessProfileSettingsSnapshot> Snapshots { get; set; } = [];
}
