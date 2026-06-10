using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class Windows8021xTaskPayloadHydrator : IWindows8021xTaskPayloadHydrator
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly IWindows8021xPayloadBuilder _payloadBuilder;

    public Windows8021xTaskPayloadHydrator(
        IntellinodeDbContext dbContext,
        IWindows8021xPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _payloadBuilder = payloadBuilder;
    }

    public bool CanHydrate(string moduleName) =>
        string.Equals(moduleName, Windows8021xModuleConstants.ModuleName, StringComparison.OrdinalIgnoreCase);

    public async Task<string?> HydrateFunctionParameterAsync(
        string storedFunctionParameter,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        if (!_payloadBuilder.TryParseCompactTaskReference(storedFunctionParameter, out var settingsVersion))
        {
            return null;
        }

        var snapshot = await _dbContext.DeviceWindows8021xSettingsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.SettingsJson))
        {
            return _payloadBuilder.BuildAgentPayload(snapshot.SettingsJson);
        }

        var live = await _dbContext.DeviceWindows8021xSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (live is not null && !string.IsNullOrWhiteSpace(live.SettingsJson))
        {
            return _payloadBuilder.BuildAgentPayload(live.SettingsJson);
        }

        return null;
    }
}
