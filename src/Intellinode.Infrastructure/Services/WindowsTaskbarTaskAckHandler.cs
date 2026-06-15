using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsTaskbarTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<WindowsTaskbarTaskAckHandler> _logger;

    public WindowsTaskbarTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<WindowsTaskbarTaskAckHandler> logger)
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
        if (!IsTaskbarTask(task))
        {
            return;
        }

        if (IsLiveReadTask(task))
        {
            await ApplyLiveReadAckAsync(device, task, terminalStatus, reason, cancellationToken);
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var settings = await ResolveTaskbarSettingsAsync(device, cancellationToken);
        if (settings is null)
        {
            _logger.LogWarning(
                "Taskbar task {TaskId} ack ignored: no device_windows_taskbar_settings row for device {DeviceId}",
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
                    SettingsKind.WindowsTaskbar,
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
                var failureMessage = settings.LastApplyMessage ?? "Taskbar apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.WindowsTaskbar,
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

    private async Task ApplyLiveReadAckAsync(
        Device device,
        DeviceTask task,
        DeviceTaskStatus terminalStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        switch (terminalStatus)
        {
            case DeviceTaskStatus.Completed:
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.WindowsTaskbar,
                    version: 0,
                    applyMode: "live-read",
                    SettingsApplyStatus.Applied,
                    adminId: null,
                    message: "Taskbar live read completed.",
                    cancellationToken,
                    task.Id,
                    task.LegacyTaskId);
                break;

            case DeviceTaskStatus.Failed:
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.WindowsTaskbar,
                    version: 0,
                    applyMode: "live-read",
                    SettingsApplyStatus.Failed,
                    adminId: null,
                    message: TruncateReason(reason) ?? "Taskbar live read failed.",
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

    internal static bool IsTaskbarTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            WindowsTaskbarModuleConstants.ModuleName,
            StringComparison.OrdinalIgnoreCase);

    internal static bool IsLiveReadTask(DeviceTask task) =>
        IsTaskbarTask(task) &&
        string.Equals(
            task.FunctionName,
            WindowsTaskbarModuleConstants.LiveReadFunctionName,
            StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName) =>
        WindowsTaskbarModuleConstants.MapApplyMode(functionName);

    private async Task<DeviceWindowsTaskbarSettings?> ResolveTaskbarSettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.WindowsTaskbarSettings is not null)
        {
            return device.WindowsTaskbarSettings;
        }

        var settings = await _dbContext.DeviceWindowsTaskbarSettings
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id, cancellationToken);
        if (settings is not null)
        {
            device.WindowsTaskbarSettings = settings;
        }

        return settings;
    }
}
