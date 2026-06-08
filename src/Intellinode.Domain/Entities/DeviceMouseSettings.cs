namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows mouse settings per device (FusionX <c>Mouse_Details</c>).
/// </summary>
public sealed class DeviceMouseSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public bool Swap { get; set; }
    public int PointerSpeed { get; set; }
    public int DoubleClickSpeed { get; set; }
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
