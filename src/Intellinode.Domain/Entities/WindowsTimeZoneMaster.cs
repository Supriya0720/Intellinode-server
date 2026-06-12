namespace Intellinode.Domain.Entities;

public class WindowsTimeZoneMaster
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string WindowsTzKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
