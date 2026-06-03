using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public sealed class DeviceSettingsApplyLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public SettingsKind SettingsKind { get; set; }
    public long SettingsVersion { get; set; }
    public string ApplyMode { get; set; } = "instant";
    public SettingsApplyStatus Status { get; set; }
    public Guid? InitiatedBy { get; set; }
    public string? Message { get; set; }
    public Guid? TaskId { get; set; }
    public int? LegacyTaskId { get; set; }
    public DeviceTask? Task { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
