namespace Intellinode.Domain.Entities;

public sealed class WindowsPowerPlanMaster
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
