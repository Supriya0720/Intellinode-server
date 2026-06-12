using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsRegionalFormatPayloadBuilder : IWindowsRegionalFormatPayloadBuilder
{
    public const int MaxFunctionParameterLength = 512;

    private readonly WindowsRegionalFormatOptions _options;

    public WindowsRegionalFormatPayloadBuilder(IOptions<WindowsRegionalFormatOptions> options)
    {
        _options = options.Value;
    }

    public string BuildPayload(WindowsRegionalFormatPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new
        {
            strTimeFormat = request.TimeFormat,
            strTimeSeperator = request.TimeSeparator,
            strAMsymbol = request.AmSymbol,
            strPMsymbol = request.PmSymbol,
            strMinyear = string.Empty,
            strMaxyear = string.Empty,
            strShortDateFormat = request.ShortDateFormat,
            strDateSeperator = request.DateSeparator,
            strLongDateFormat = request.LongDateFormat,
            strShortDateSample = request.ShortDateSample,
            strLongDateSample = request.LongDateSample,
            TaskID = request.TaskID,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { RegionalSettings = settings } });
    }

    public string GetModuleName() => WindowsRegionalFormatModuleConstants.ModuleName;

    public string GetSignalSuffix() =>
        string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? WindowsRegionalFormatModuleConstants.DefaultSignalSuffix
            : _options.DefaultSignalSuffix.Trim();
}
