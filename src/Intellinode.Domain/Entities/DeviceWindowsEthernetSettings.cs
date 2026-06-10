namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows Ethernet settings per device (FusionX Network Settings → Ethernet).
/// </summary>
public sealed class DeviceWindowsEthernetSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public bool IsDhcp { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string PrimaryWins { get; set; } = string.Empty;
    public string SecondaryWins { get; set; } = string.Empty;
    public bool ObtainDnsAutomatically { get; set; }
    public string NetworkSpeed { get; set; } = "AutoSelect";
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
