namespace Intellinode.Domain.Entities;

/// <summary>
/// FusionX AdvancePowerOption.aspx dropdown catalog entry (display label + agent value).
/// </summary>
public sealed class WindowsPowerAdvancedOptionMaster
{
    public int Id { get; set; }

    /// <summary>Null = all power plans.</summary>
    public string? PlanName { get; set; }

    public string OptionName { get; set; } = string.Empty;
    public string SettingName { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public string ValueText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
