using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public class HeartbeatBindingChange
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public bool IsServiceMode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ChangedValue { get; set; } = string.Empty;
    public HeartbeatBindingKind Kind { get; set; }
    public bool IsBindingActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
