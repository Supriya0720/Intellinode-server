using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsWirelessSetupTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<WindowsWirelessSetupTaskAckHandler> _logger;

    public WindowsWirelessSetupTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<WindowsWirelessSetupTaskAckHandler> logger)
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
        if (!IsWirelessSetupTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var settings = await ResolveWirelessSetupSettingsAsync(device, cancellationToken);
        if (settings is null)
        {
            _logger.LogWarning(
                "Wireless setup task {TaskId} ack ignored: no device_windows_wireless_setup_settings row for device {DeviceId}",
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
                    SettingsKind.WindowsWirelessSetup,
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
                var failureMessage = settings.LastApplyMessage ?? "Wireless setup apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.WindowsWirelessSetup,
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

    public static bool IsWirelessSetupTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            WindowsWirelessSetupModuleConstants.ModuleName,
            StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsWirelessSetupModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsWirelessSetupModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "queued";
        }

        return "queued";
    }

    private async Task<DeviceWindowsWirelessSetupSettings?> ResolveWirelessSetupSettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.WindowsWirelessSetupSettings is not null)
        {
            return device.WindowsWirelessSetupSettings;
        }

        var settings = await _dbContext.DeviceWindowsWirelessSetupSettings
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id, cancellationToken);
        if (settings is not null)
        {
            device.WindowsWirelessSetupSettings = settings;
        }

        return settings;
    }
}
