namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows region and location (geo + language locale) per device.
/// FusionX System Settings → Time and Language → Region &amp; Location.
/// </summary>
public sealed class DeviceWindowsRegionLocationSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public int GeoId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int LanguageCode { get; set; }
    public string Bcp47Code { get; set; } = string.Empty;
    public string LanguageDescription { get; set; } = string.Empty;
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
