namespace Intellinode.Application.Validation;

/// <summary>
/// PR3 optional command denylist and validation policy (bound from <c>WindowsApplicationCommand</c> appsettings section).
/// </summary>
public sealed class WindowsApplicationCommandValidationPolicy
{
    public const string SectionName = "WindowsApplicationCommand";

    public bool CommandDenylistEnabled { get; set; } = true;

    /// <summary>
    /// Case-insensitive substring patterns rejected in Command mode (FusionX UI warns against shutdown).
    /// </summary>
    public string[] DeniedCommandPatterns { get; set; } = ["shutdown"];
}
