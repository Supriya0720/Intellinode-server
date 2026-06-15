namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows user interface (autologon) settings per device
/// (FusionX User Settings → User Interface, ModuleType "Autologon").
/// </summary>
public sealed class DeviceWindowsUserInterfaceSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string UserName { get; set; } = string.Empty;
    public bool AutoLogon { get; set; }
    /// <summary>Encrypted Windows password for autologon; never exposed via admin read APIs.</summary>
    public string? PasswordCipher { get; set; }
    public int AgentAction { get; set; }
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
