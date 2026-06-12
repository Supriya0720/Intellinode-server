using Intellinode.Domain.Enums;

namespace Intellinode.Domain.Entities;

public sealed class WindowsPowerTimeoutMaster
{
    public int Id { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public int? ValueSeconds { get; set; }
    public WindowsPowerTimeoutCategory Category { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
