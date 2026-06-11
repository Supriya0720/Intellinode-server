namespace Intellinode.Domain.Entities;

/// <summary>
/// Immutable settings JSON captured at queue time for a specific profile and <see cref="SettingsVersion"/>.
/// Enables agent hydration even after the live profile row advances to a newer version.
/// </summary>
public sealed class DeviceWindowsWirelessProfileSettingsSnapshot
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public long ProfileKey { get; set; }
    public DeviceWindowsWirelessProfileSettings Profile { get; set; } = null!;
    public long SettingsVersion { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
