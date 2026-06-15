using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Expands compact <c>{"settingsVersion":N}</c> task references into full
/// <c>WinCELinux.XPAutologon</c> JSON for queued/template applies.
/// </summary>
public sealed class WindowsUserInterfaceTaskPayloadHydrator : IWindowsUserInterfaceTaskPayloadHydrator
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly IWindowsUserInterfacePayloadBuilder _payloadBuilder;
    private readonly IWindowsUserInterfacePasswordProtector _passwordProtector;

    public WindowsUserInterfaceTaskPayloadHydrator(
        IntellinodeDbContext dbContext,
        IWindowsUserInterfacePayloadBuilder payloadBuilder,
        IWindowsUserInterfacePasswordProtector passwordProtector)
    {
        _dbContext = dbContext;
        _payloadBuilder = payloadBuilder;
        _passwordProtector = passwordProtector;
    }

    public bool CanHydrate(string moduleName) =>
        string.Equals(moduleName, WindowsUserInterfaceModuleConstants.ModuleName, StringComparison.OrdinalIgnoreCase);

    public async Task<string?> HydrateFunctionParameterAsync(
        string storedFunctionParameter,
        Guid deviceId,
        int legacyTaskId = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_payloadBuilder.TryParseCompactTaskReference(storedFunctionParameter, out var settingsVersion))
        {
            return null;
        }

        var snapshot = await _dbContext.DeviceWindowsUserInterfaceSettingsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (snapshot is not null)
        {
            if (!_passwordProtector.TryUnprotect(snapshot.PasswordCipher, out var password))
            {
                return null;
            }

            return _payloadBuilder.BuildAgentPayload(
                _payloadBuilder.MapToPayloadRequest(snapshot, legacyTaskId, snapshot.AgentAction, password));
        }

        var settings = await _dbContext.DeviceWindowsUserInterfaceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (settings is null || !_passwordProtector.TryUnprotect(settings.PasswordCipher, out var settingsPassword))
        {
            return null;
        }

        return _payloadBuilder.BuildAgentPayload(
            _payloadBuilder.MapToPayloadRequest(settings, legacyTaskId, settings.AgentAction, settingsPassword));
    }
}
