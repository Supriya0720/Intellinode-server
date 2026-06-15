namespace Intellinode.Application.Validation;

/// <summary>
/// FusionX User Interface / Autologon apply-block reason codes and display messages.
/// Mirrors <c>WindowsUserInterface.cs</c> Autologon + Pending/In process responses.
/// </summary>
public static class WindowsUserInterfaceApplyBlockReason
{
    public const string PendingTaskExists = "PendingTaskExists";
    public const string InProcessTaskExists = "InProcessTaskExists";
    public const string EnrollmentStateBlocked = "EnrollmentStateBlocked";

    /// <summary>Machine-readable reason for bulk/group per-target results.</summary>
    public static string MapBulkBlockReason(string? errorCode, string? message)
    {
        if (string.Equals(errorCode, "ApplyBlocked", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(message))
        {
            return message.Trim();
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            return errorCode;
        }

        return string.IsNullOrWhiteSpace(message) ? "ApplyBlocked" : message.Trim();
    }

    /// <summary>FusionX-style admin message for single-target 409 ApplyBlocked responses.</summary>
    public static string FormatFusionXMessage(string? reasonCode) =>
        reasonCode switch
        {
            PendingTaskExists => "Autologon settings are pending",
            InProcessTaskExists => "Autologon settings are in process",
            EnrollmentStateBlocked => "Autologon apply is blocked by enrollment state",
            _ => string.IsNullOrWhiteSpace(reasonCode) ? "Apply is blocked for this device." : reasonCode
        };
}
