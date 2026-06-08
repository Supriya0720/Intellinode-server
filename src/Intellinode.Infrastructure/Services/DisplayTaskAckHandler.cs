using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class DisplayTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<DisplayTaskAckHandler> _logger;

    public DisplayTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<DisplayTaskAckHandler> logger)
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
        if (!IsDisplayTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var display = await ResolveDisplaySettingsAsync(device, cancellationToken);
        if (display is null)
        {
            _logger.LogWarning(
                "Display task {TaskId} ack ignored: no device_display_settings row for device {DeviceId}",
                task.Id,
                device.Id);
            return;
        }

        var now = DateTime.UtcNow;
        switch (terminalStatus)
        {
            case DeviceTaskStatus.Completed:
                display.LastAppliedVersion = display.SettingsVersion;
                display.LastAppliedUtc = now;
                display.PendingApply = false;
                display.LastApplyStatus = "Applied";
                display.LastApplyMessage = null;
                display.UpdatedUtc = now;
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Display,
                    display.SettingsVersion,
                    "instant",
                    SettingsApplyStatus.Applied,
                    adminId: null,
                    message: null,
                    cancellationToken,
                    task.Id,
                    task.LegacyTaskId);
                break;

            case DeviceTaskStatus.Failed:
                display.PendingApply = false;
                display.LastApplyStatus = "Failed";
                display.LastApplyMessage = TruncateReason(reason);
                display.UpdatedUtc = now;
                var failureMessage = display.LastApplyMessage ?? "Display apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Display,
                    display.SettingsVersion,
                    "instant",
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

    internal static bool IsDisplayTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            DisplaySettingsService.DisplayModuleName,
            StringComparison.OrdinalIgnoreCase);

    private async Task<DeviceDisplaySettings?> ResolveDisplaySettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.DisplaySettings is not null)
        {
            return device.DisplaySettings;
        }

        var display = await _dbContext.DeviceDisplaySettings
            .FirstOrDefaultAsync(k => k.DeviceId == device.Id, cancellationToken);
        if (display is not null)
        {
            device.DisplaySettings = display;
        }

        return display;
    }
}
