namespace Intellinode.Application.Validation;

/// <summary>
/// FusionX Application command apply-block reason codes and display messages.
/// </summary>
public static class WindowsApplicationCommandApplyBlockReason
{
    public const string PendingTaskExists = "PendingTaskExists";
    public const string InProcessTaskExists = "InProcessTaskExists";
    public const string EnrollmentStateBlocked = "EnrollmentStateBlocked";

    public static string FormatFusionXMessage(string? reasonCode) =>
        reasonCode switch
        {
            PendingTaskExists => "Application or command settings are pending",
            InProcessTaskExists => "Application or command settings are in process",
            EnrollmentStateBlocked => "Application command apply is blocked by enrollment state",
            _ => string.IsNullOrWhiteSpace(reasonCode) ? "Apply is blocked for this device." : reasonCode
        };

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
}
