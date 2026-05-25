namespace Intellinode.Domain.Entities;

public class DeviceTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public int LegacyTaskId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string FunctionParameter { get; set; } = string.Empty;
    public string ExtraData { get; set; } = string.Empty;
    public DeviceTaskStatus Status { get; set; } = DeviceTaskStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
}

public enum DeviceTaskStatus
{
    Pending = 0,
    InProcess = 1,
    Completed = 2,
    Failed = 3
}
