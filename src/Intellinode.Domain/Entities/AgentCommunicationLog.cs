namespace Intellinode.Domain.Entities;

public sealed class AgentCommunicationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string? MacAddress { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string? PayloadSummary { get; set; }
    public string? CommandCode { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
