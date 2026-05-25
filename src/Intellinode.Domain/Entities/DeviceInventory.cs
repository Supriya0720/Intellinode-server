namespace Intellinode.Domain.Entities;

public sealed class DeviceInventory
{
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public string? HardwareJson { get; set; }
    public string? NetworkJson { get; set; }
    public string? OsInfoJson { get; set; }
    public string? SecurityJson { get; set; }
    public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}
