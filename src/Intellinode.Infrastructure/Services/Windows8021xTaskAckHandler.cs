using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class Windows8021xTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<Windows8021xTaskAckHandler> _logger;

    public Windows8021xTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<Windows8021xTaskAckHandler> logger)
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
        if (!IsWindows8021xTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var settings = await ResolveWindows8021xSettingsAsync(device, cancellationToken);
        if (settings is null)
        {
            _logger.LogWarning(
                "Windows 802.1X task {TaskId} ack ignored: no device_windows_802_1x_settings row for device {DeviceId}",
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
                    SettingsKind.Windows8021x,
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
                var failureMessage = settings.LastApplyMessage ?? "Windows 802.1X apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Windows8021x,
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

    public static bool IsWindows8021xTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            Windows8021xModuleConstants.ModuleName,
            StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, Windows8021xModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, Windows8021xModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "queued";
        }

        return "queued";
    }

    private async Task<DeviceWindows8021xSettings?> ResolveWindows8021xSettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.Windows8021xSettings is not null)
        {
            return device.Windows8021xSettings;
        }

        var settings = await _dbContext.DeviceWindows8021xSettings
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id, cancellationToken);
        if (settings is not null)
        {
            device.Windows8021xSettings = settings;
        }

        return settings;
    }
}
