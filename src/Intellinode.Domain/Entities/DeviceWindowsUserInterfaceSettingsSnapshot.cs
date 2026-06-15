namespace Intellinode.Domain.Entities;

/// <summary>
/// Immutable autologon state captured at queue time for a specific <see cref="SettingsVersion"/>.
/// Enables agent hydration for queued/template tasks after the live row advances.
/// </summary>
public sealed class DeviceWindowsUserInterfaceSettingsSnapshot
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public long SettingsVersion { get; set; }
    public string UserName { get; set; } = string.Empty;
    public bool AutoLogon { get; set; }
    public string? PasswordCipher { get; set; }
    public int AgentAction { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
