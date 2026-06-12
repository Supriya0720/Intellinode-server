using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsRegionLocationPayloadBuilder : IWindowsRegionLocationPayloadBuilder
{
    public const int MaxFunctionParameterLength = 512;

    private readonly WindowsRegionLocationOptions _options;

    public WindowsRegionLocationPayloadBuilder(IOptions<WindowsRegionLocationOptions> options)
    {
        _options = options.Value;
    }

    public string BuildPayload(WindowsRegionLocationPayloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = new
        {
            GeoID = request.GeoId,
            Location = request.LocationName,
            BCP47Code = request.Bcp47Code,
            LanguageCode = request.LanguageCode,
            LanguageDescription = request.LanguageDescription,
            TaskID = request.TaskID,
            AgentAction = request.AgentAction
        };

        return JsonSerializer.Serialize(new { WinCELinux = new { RegionAndLocation = settings } });
    }

    public string GetModuleName() => WindowsRegionLocationModuleConstants.ModuleName;

    public string GetSignalSuffix() =>
        string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? WindowsRegionLocationModuleConstants.DefaultSignalSuffix
            : _options.DefaultSignalSuffix.Trim();
}
