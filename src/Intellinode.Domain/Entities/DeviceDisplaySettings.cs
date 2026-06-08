namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows display settings per device (FusionX <c>Display_Details</c>).
/// </summary>
public sealed class DeviceDisplaySettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string Resolution { get; set; } = string.Empty;
    public string ColorDepth { get; set; } = string.Empty;
    public string DualDisplayOption { get; set; } = string.Empty;
    public string SecondaryRotation { get; set; } = string.Empty;
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
