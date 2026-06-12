using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsPowerManagementTaskPayloadHydrator : IWindowsPowerManagementTaskPayloadHydrator
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly IWindowsPowerManagementPayloadBuilder _payloadBuilder;

    public WindowsPowerManagementTaskPayloadHydrator(
        IntellinodeDbContext dbContext,
        IWindowsPowerManagementPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _payloadBuilder = payloadBuilder;
    }

    public bool CanHydrate(string moduleName) =>
        string.Equals(moduleName, WindowsPowerManagementModuleConstants.ModuleName, StringComparison.OrdinalIgnoreCase);

    public async Task<string?> HydrateFunctionParameterAsync(
        string storedFunctionParameter,
        Guid deviceId,
        int legacyTaskId = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_payloadBuilder.TryParseCompactTaskReference(
                storedFunctionParameter,
                out var settingsVersion,
                out var planName))
        {
            return null;
        }

        var snapshot = await _dbContext.DeviceWindowsPowerManagementSettingsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.SettingsJson))
        {
            if (!string.IsNullOrWhiteSpace(planName) &&
                !string.Equals(snapshot.ActivePlanName, planName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return _payloadBuilder.BuildAgentPayload(
                snapshot.SettingsJson,
                legacyTaskId,
                snapshot.AgentAction);
        }

        var settings = await _dbContext.DeviceWindowsPowerManagementSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (settings is null || string.IsNullOrWhiteSpace(settings.SettingsJson))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(planName) &&
            !string.Equals(settings.ActivePlanName, planName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return _payloadBuilder.BuildAgentPayload(
            settings.SettingsJson,
            legacyTaskId,
            settings.AgentAction);
    }
}
