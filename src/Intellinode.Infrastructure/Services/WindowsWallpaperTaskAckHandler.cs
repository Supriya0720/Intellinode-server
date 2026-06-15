using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsWallpaperTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<WindowsWallpaperTaskAckHandler> _logger;

    public WindowsWallpaperTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<WindowsWallpaperTaskAckHandler> logger)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task ApplyAckAsync(
        Device device,
        DeviceTask task,
        DeviceTaskStatus terminalStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!IsWallpaperTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var settings = await ResolveWallpaperSettingsAsync(device, cancellationToken);
        if (settings is null)
        {
            _logger.LogWarning(
                "Wallpaper task {TaskId} ack ignored: no device_windows_wallpaper_settings row for device {DeviceId}",
                task.Id,
                device.Id);
            return;
        }

        var applyMode = MapTaskApplyMode(task.FunctionName);
        var now = DateTime.UtcNow;

        switch (terminalStatus)
        {
            case DeviceTaskStatus.Completed:
                settings.LastAppliedVersion = settings.SettingsVersion;
                settings.LastAppliedUtc = now;
                settings.PendingApply = false;
                settings.LastApplyStatus = "Applied";
                settings.LastApplyMessage = null;
                settings.UpdatedUtc = now;
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.WindowsWallpaper,
                    settings.SettingsVersion,
                    applyMode,
                    SettingsApplyStatus.Applied,
                    adminId: null,
                    message: null,
                    cancellationToken,
                    task.Id,
                    task.LegacyTaskId);
                break;

            case DeviceTaskStatus.Failed:
                settings.PendingApply = false;
                settings.LastApplyStatus = "Failed";
                settings.LastApplyMessage = TruncateReason(reason);
                settings.UpdatedUtc = now;
                var failureMessage = settings.LastApplyMessage ?? "Wallpaper apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.WindowsWallpaper,
                    settings.SettingsVersion,
                    applyMode,
                    SettingsApplyStatus.Failed,
                    adminId: null,
                    message: failureMessage,
                    cancellationToken,
                    task.Id,
                    task.LegacyTaskId);
                break;
        }
    }

    public static string? TruncateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        return trimmed.Length <= MaxReasonLength
            ? trimmed
            : trimmed[..MaxReasonLength];
    }

    internal static bool IsWallpaperTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            WindowsWallpaperModuleConstants.ModuleName,
            StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName) =>
        WindowsWallpaperModuleConstants.MapApplyMode(functionName);

    private async Task<DeviceWindowsWallpaperSettings?> ResolveWallpaperSettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.WindowsWallpaperSettings is not null)
        {
            return device.WindowsWallpaperSettings;
        }

        var settings = await _dbContext.DeviceWindowsWallpaperSettings
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id, cancellationToken);
        if (settings is not null)
        {
            device.WindowsWallpaperSettings = settings;
        }

        return settings;
    }
}
