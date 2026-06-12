using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsDateTimePayloadBuilder : IWindowsDateTimePayloadBuilder
{
    public const int MaxFunctionParameterLength = 512;

    private readonly WindowsDateTimeOptions _options;

    public WindowsDateTimePayloadBuilder(IOptions<WindowsDateTimeOptions> options)
    {
        _options = options.Value;
    }

    public string BuildPayload(WindowsDateTimePayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var strTimeZone = string.Empty;
        var dtDate = string.Empty;
        var dtTime = string.Empty;
        var timeServer = string.Empty;
        var muiDisplay = string.Empty;

        switch (request.ApplyMode)
        {
            case WindowsDateTimeApplyMode.ManualDateTime:
                if (request.CurrentDateLocal is { } date && request.CurrentTimeLocal is { } time)
                {
                    dtDate = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0)
                        .ToString("yyyy-MM-ddTHH:mm:ss");
                    dtTime = new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second)
                        .ToString("yyyy-MM-ddTHH:mm:ss");
                }

                break;

            case WindowsDateTimeApplyMode.TimeZone:
                strTimeZone = request.TimeZoneDisplay;
                muiDisplay = request.WindowsTzKey;
                break;

            case WindowsDateTimeApplyMode.TimeServer:
                timeServer = request.TimeServer;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.ApplyMode, "Unsupported date/time apply mode.");
        }

        var settings = new
        {
            strTimeZone,
            DtDate = dtDate,
            DtTime = dtTime,
            TimeServer = timeServer,
            MUI_Display = muiDisplay,
            TaskID = request.TaskID,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { XPDATE_TIME = settings } });
    }

    public string GetModuleNameForApplyMode(WindowsDateTimeApplyMode mode) =>
        mode switch
        {
            WindowsDateTimeApplyMode.ManualDateTime => WindowsDateTimeModuleConstants.DateTimeModuleName,
            WindowsDateTimeApplyMode.TimeZone => WindowsDateTimeModuleConstants.TimeZoneModuleName,
            WindowsDateTimeApplyMode.TimeServer => WindowsDateTimeModuleConstants.TimeServerModuleName,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported date/time apply mode.")
        };

    public string GetSignalSuffixForApplyMode(WindowsDateTimeApplyMode mode) =>
        mode switch
        {
            WindowsDateTimeApplyMode.ManualDateTime => ResolveSuffix(
                _options.ManualDateTimeSignalSuffix,
                WindowsDateTimeModuleConstants.DefaultManualDateTimeSignalSuffix),
            WindowsDateTimeApplyMode.TimeZone => ResolveSuffix(
                _options.TimeZoneSignalSuffix,
                WindowsDateTimeModuleConstants.DefaultTimeZoneSignalSuffix),
            WindowsDateTimeApplyMode.TimeServer => ResolveSuffix(
                _options.TimeServerSignalSuffix,
                WindowsDateTimeModuleConstants.DefaultTimeServerSignalSuffix),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported date/time apply mode.")
        };

    private static string ResolveSuffix(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
}
