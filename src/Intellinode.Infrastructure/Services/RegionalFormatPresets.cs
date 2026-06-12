using Intellinode.Application.Contracts.Admin;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Static FusionX-style regional format tokens for admin UI dropdowns (no DB table).
/// </summary>
public static class RegionalFormatPresets
{
    public static RegionalFormatPresetsResponse GetPresets() =>
        new()
        {
            Data = new RegionalFormatPresetsData
            {
                ShortDateFormats =
                [
                    "dd/MM/yyyy",
                    "MM/dd/yyyy",
                    "yyyy-MM-dd",
                    "dd-MM-yyyy",
                    "d/M/yyyy"
                ],
                LongDateFormats =
                [
                    "dddd, MMMM dd, yyyy",
                    "dddd, dd MMMM yyyy",
                    "MMMM dd, yyyy",
                    "dddd, d MMMM yyyy"
                ],
                TimeFormats =
                [
                    "HH:mm:ss",
                    "H:mm:ss",
                    "hh:mm:ss tt",
                    "h:mm:ss tt",
                    "HH:mm",
                    "h:mm tt"
                ],
                DateSeparators = ["/", "-", ".", " "],
                TimeSeparators = [":", ".", " "]
            }
        };
}
