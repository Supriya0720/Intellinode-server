namespace Intellinode.Domain.Entities;

/// <summary>
/// Immutable screen saver state captured at queue time for a specific <see cref="SettingsVersion"/>.
/// Enables agent hydration for repository/upload tasks after the live row advances.
/// </summary>
public sealed class DeviceWindowsScreenSaverSettingsSnapshot
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public long SettingsVersion { get; set; }
    public string ScreenSaverName { get; set; } = string.Empty;
    public int TimeoutMinutes { get; set; }
    public bool PasswordProtected { get; set; }
    public bool PreventUserChanges { get; set; }
    public string SourceType { get; set; } = "Browse";
    public bool Upload { get; set; }
    public int AgentAction { get; set; }
    public string? RepositoryJson { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
