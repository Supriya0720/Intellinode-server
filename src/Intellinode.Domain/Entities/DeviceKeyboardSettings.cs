namespace Intellinode.Domain.Entities;

/// <summary>
/// Desired/applied Windows keyboard settings per device (FusionX <c>Keyboard_Details</c>).
/// </summary>
public sealed class DeviceKeyboardSettings
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public int Delay { get; set; }
    public int RepeatRate { get; set; }
    public string KeyboardLocale { get; set; } = string.Empty;
    public bool ReplaceExistingKeyboard { get; set; }
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
