using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows computer name and domain join settings per device (FusionX Network Settings → Computer Name).
/// </summary>
public sealed class DeviceWindowsComputerNameSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public ComputerNameApplyMode ApplyMode { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string WorkGroup { get; set; } = string.Empty;
    public string OrganizationalUnit { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsDomainJoin { get; set; }
    public string Prefix { get; set; } = string.Empty;
    public string Postfix { get; set; } = string.Empty;
    public int NoOfChar { get; set; }
    public bool IsMacOrSerial { get; set; }
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
