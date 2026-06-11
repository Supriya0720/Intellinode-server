using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsWirelessPropertiesTaskPayloadHydrator : IWindowsWirelessPropertiesTaskPayloadHydrator
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly IWindowsWirelessPropertiesPayloadBuilder _payloadBuilder;

    public WindowsWirelessPropertiesTaskPayloadHydrator(
        IntellinodeDbContext dbContext,
        IWindowsWirelessPropertiesPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _payloadBuilder = payloadBuilder;
    }

    public bool CanHydrate(string moduleName) =>
        string.Equals(moduleName, WindowsWirelessPropertiesModuleConstants.ModuleName, StringComparison.OrdinalIgnoreCase);

    public async Task<string?> HydrateFunctionParameterAsync(
        string storedFunctionParameter,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!_payloadBuilder.TryParseCompactTaskReference(
                storedFunctionParameter,
                out var settingsVersion,
                out var profileKey))
        {
            return null;
        }

        var snapshot = await _dbContext.DeviceWindowsWirelessProfileSettingsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId &&
                     s.ProfileKey == profileKey &&
                     s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.SettingsJson))
        {
            return _payloadBuilder.BuildAgentPayload(snapshot.SettingsJson);
        }

        var live = await _dbContext.DeviceWindowsWirelessProfileSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId &&
                     s.ProfileKey == profileKey &&
                     s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (live is not null && !string.IsNullOrWhiteSpace(live.SettingsJson))
        {
            return _payloadBuilder.BuildAgentPayload(live.SettingsJson);
        }

        return null;
    }
}
