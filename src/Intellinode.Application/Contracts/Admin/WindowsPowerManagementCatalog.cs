namespace Intellinode.Application.Contracts.Admin;

/// <summary>
/// FusionX advanced power option catalog helpers (AdvancePowerOption.aspx / spike scenario 5).
/// </summary>
public static class WindowsPowerManagementCatalog
{
    public static readonly HashSet<string> BasicOptionGroupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display",
        "Hard disk",
        "Sleep",
        "Power buttons and lid",
        "System standby"
    };

    public static readonly HashSet<string> AdvancedOnlyOptionGroupNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Require a password on wakeup",
        "Slide show",
        "Power saving mode",
        "USB selective suspend setting",
        "Link state power management",
        "Minimum processor state",
        "System cooling policy",
        "Maximum processor state",
        "When sharing media",
        "When playing video"
    };

    public static readonly HashSet<string> AdvancedSleepSettingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Allow hybrid sleep",
        "Allow wake timers",
        "Hibernate after"
    };

    public static readonly HashSet<string> BasicSleepSettingNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sleep after"
    };

    public static bool IsAdvancedOptionGroup(WindowsPowerManagementOptionGroup group)
    {
        if (AdvancedOnlyOptionGroupNames.Contains(group.OptionName))
        {
            return true;
        }

        if (!string.Equals(group.OptionName, "Sleep", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return group.Settings.Any(s => AdvancedSleepSettingNames.Contains(s.SettingName));
    }

    public static bool ContainsAdvancedOption(IEnumerable<WindowsPowerManagementOptionGroup> groups) =>
        groups.Any(IsAdvancedOptionGroup);
}
