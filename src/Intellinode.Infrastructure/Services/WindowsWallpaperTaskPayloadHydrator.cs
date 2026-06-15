using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

/// <summary>
/// Expands compact <c>{"settingsVersion":N}</c> task references into full
/// <c>WinCELinux.XPWallPaper</c> JSON for repository/upload applies (ADR-0006 Option B).
/// Browse-path tasks store inline JSON and are returned unchanged.
/// </summary>
public sealed class WindowsWallpaperTaskPayloadHydrator : IWindowsWallpaperTaskPayloadHydrator
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly IWindowsWallpaperPayloadBuilder _payloadBuilder;

    public WindowsWallpaperTaskPayloadHydrator(
        IntellinodeDbContext dbContext,
        IWindowsWallpaperPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _payloadBuilder = payloadBuilder;
    }

    public bool CanHydrate(string moduleName) =>
        string.Equals(moduleName, WindowsWallpaperModuleConstants.ModuleName, StringComparison.OrdinalIgnoreCase);

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

        var snapshot = await _dbContext.DeviceWindowsWallpaperSettingsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (snapshot is not null)
        {
            return _payloadBuilder.BuildAgentPayload(
                _payloadBuilder.MapToPayloadRequest(snapshot, legacyTaskId, snapshot.AgentAction));
        }

        var settings = await _dbContext.DeviceWindowsWallpaperSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == deviceId && s.SettingsVersion == settingsVersion,
                cancellationToken);

        if (settings is null || !settings.Upload && string.IsNullOrWhiteSpace(settings.RepositoryJson))
        {
            return null;
        }

        return _payloadBuilder.BuildAgentPayload(
            _payloadBuilder.MapToPayloadRequest(settings, legacyTaskId, settings.AgentAction));
    }
}
