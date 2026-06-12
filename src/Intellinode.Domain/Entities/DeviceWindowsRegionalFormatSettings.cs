namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows regional date/time display format per device.
/// FusionX System Settings → Time and Language → Date &amp; Time Format.
/// </summary>
public sealed class DeviceWindowsRegionalFormatSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string TimeFormat { get; set; } = string.Empty;
    public string TimeSeparator { get; set; } = string.Empty;
    public string AmSymbol { get; set; } = string.Empty;
    public string PmSymbol { get; set; } = string.Empty;
    public string ShortDateFormat { get; set; } = string.Empty;
    public string DateSeparator { get; set; } = string.Empty;
    public string LongDateFormat { get; set; } = string.Empty;
    public string ShortDateSample { get; set; } = string.Empty;
    public string LongDateSample { get; set; } = string.Empty;
    public string? TimeSample { get; set; }
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
