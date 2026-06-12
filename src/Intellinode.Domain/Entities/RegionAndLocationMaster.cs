namespace Intellinode.Domain.Entities;

public class RegionAndLocationMaster
{
    public int Id { get; set; }
    public char Identifier { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Bcp47Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
