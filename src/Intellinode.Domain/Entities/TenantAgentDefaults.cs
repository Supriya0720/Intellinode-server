using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public sealed class TenantAgentDefaults
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string ServerBaseUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public int DefaultPollIntervalSeconds { get; set; } = 300;
    public CommunicationType DefaultCommunicationType { get; set; } = CommunicationType.HTTPS;
    public int MinPollIntervalHttp { get; set; } = 30;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
