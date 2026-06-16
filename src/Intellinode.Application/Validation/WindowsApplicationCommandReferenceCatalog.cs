using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Application.Validation;

/// <summary>
/// FusionX <c>Windows_ucAplicationAndCommand</c> dropdown values (MUI + ascx parity).
/// </summary>
public static class WindowsApplicationCommandReferenceCatalog
{
    private static readonly HashSet<string> MessageTypeValues = new(StringComparer.Ordinal)
    {
        "0",
        "1"
    };

    private static readonly HashSet<string> DisplayTimeValues = new(StringComparer.Ordinal)
    {
        "60", "120", "180", "240", "300", "360", "420", "480", "540", "600"
    };

    private static readonly HashSet<string> TimeoutValues = new(StringComparer.Ordinal)
    {
        "0", "5", "30", "60", "120", "180", "300"
    };

    public static WindowsApplicationCommandReferenceOptionsResponse GetOptions() =>
        new()
        {
            Success = true,
            Message = "Application command reference options.",
            Data = new WindowsApplicationCommandReferenceOptionsData
            {
                MessageTypes =
                [
                    new WindowsApplicationCommandReferenceItemDto
                    {
                        Value = "1",
                        Label = "Message box"
                    },
                    new WindowsApplicationCommandReferenceItemDto
                    {
                        Value = "0",
                        Label = "Information message box"
                    }
                ],
                DisplayTimes =
                [
                    Item("60", "1 Minute"),
                    Item("120", "2 Minute"),
                    Item("180", "3 Minute"),
                    Item("240", "4 Minute"),
                    Item("300", "5 Minute"),
                    Item("360", "6 Minute"),
                    Item("420", "7 Minute"),
                    Item("480", "8 Minute"),
                    Item("540", "9 Minute"),
                    Item("600", "10 Minute")
                ],
                Timeouts =
                [
                    Item("0", "Never"),
                    Item("5", "5 Second"),
                    Item("30", "30 Second"),
                    Item("60", "1 Minute"),
                    Item("120", "2 Minute"),
                    Item("180", "3 Minute"),
                    Item("300", "5 Minute")
                ]
            }
        };

    public static bool IsValidMessageType(string? value) =>
        !string.IsNullOrWhiteSpace(value) && MessageTypeValues.Contains(value.Trim());

    public static bool IsValidDisplayTime(string? value) =>
        !string.IsNullOrWhiteSpace(value) && DisplayTimeValues.Contains(value.Trim());

    public static bool IsValidTimeout(string? value) =>
        !string.IsNullOrWhiteSpace(value) && TimeoutValues.Contains(value.Trim());

    public static bool IsDeniedCommand(string commandText, WindowsApplicationCommandValidationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!policy.CommandDenylistEnabled || string.IsNullOrWhiteSpace(commandText))
        {
            return false;
        }

        var normalized = commandText.Trim();
        foreach (var pattern in policy.DeniedCommandPatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                continue;
            }

            if (normalized.Contains(pattern.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static WindowsApplicationCommandReferenceItemDto Item(string value, string label) =>
        new() { Value = value, Label = label };
}
