namespace Intellinode.Domain.Entities;

public sealed class AgentEnrollmentToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TokenHash { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid? CreatedByAdminId { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? ConsumedUtc { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
