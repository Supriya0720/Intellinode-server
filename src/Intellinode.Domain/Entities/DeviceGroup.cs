namespace Intellinode.Domain.Entities;

public class DeviceGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid? ParentGroupId { get; set; }
    public DeviceGroup? ParentGroup { get; set; }
    public ICollection<DeviceGroup> ChildGroups { get; set; } = [];
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Device> Devices { get; set; } = [];
    public GroupRemoteSettings? RemoteSettings { get; set; }
    public GroupAgentAdvancedSettings? AgentAdvancedSettings { get; set; }
}
