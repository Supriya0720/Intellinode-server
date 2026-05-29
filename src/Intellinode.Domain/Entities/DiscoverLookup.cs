using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public class DiscoverLookup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public string MacAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string OsName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string DiscoveryType { get; set; } = "AgentSelfDiscovery";
    public DiscoverLookupStatus Status { get; set; } = DiscoverLookupStatus.Pending;
    public DateTime DiscoveredUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public Guid? ApprovedByAdminId { get; set; }
    public AdminUser? ApprovedByAdmin { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public Guid? RejectedByAdminId { get; set; }
    public AdminUser? RejectedByAdmin { get; set; }
    public DateTime? RejectedUtc { get; set; }
    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }
}
