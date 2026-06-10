namespace Intellinode.Domain.Entities;

/// <summary>
/// Immutable settings JSON captured at queue time for a specific <see cref="SettingsVersion"/>.
/// Enables agent hydration even after the live device row advances to a newer version.
/// </summary>
public sealed class DeviceWindows8021xSettingsSnapshot
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public long SettingsVersion { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
