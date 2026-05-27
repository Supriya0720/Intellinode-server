namespace Intellinode.Domain.Entities;

public sealed class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DeviceGroup> DeviceGroups { get; set; } = [];
    public ICollection<Device> Devices { get; set; } = [];
    public TenantAgentDefaults? AgentDefaults { get; set; }
}
