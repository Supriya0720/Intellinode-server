using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class MouseTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<MouseTaskAckHandler> _logger;

    public MouseTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<MouseTaskAckHandler> logger)
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
        if (!IsMouseTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var mouse = await ResolveMouseSettingsAsync(device, cancellationToken);
        if (mouse is null)
        {
            _logger.LogWarning(
                "Mouse task {TaskId} ack ignored: no device_mouse_settings row for device {DeviceId}",
                task.Id,
                device.Id);
            return;
        }

        var now = DateTime.UtcNow;
        switch (terminalStatus)
        {
            case DeviceTaskStatus.Completed:
                mouse.LastAppliedVersion = mouse.SettingsVersion;
                mouse.LastAppliedUtc = now;
                mouse.PendingApply = false;
                mouse.LastApplyStatus = "Applied";
                mouse.LastApplyMessage = null;
                mouse.UpdatedUtc = now;
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Mouse,
                    mouse.SettingsVersion,
                    "instant",
                    SettingsApplyStatus.Applied,
                    adminId: null,
                    message: null,
                    cancellationToken,
                    task.Id,
                    task.LegacyTaskId);
                break;

            case DeviceTaskStatus.Failed:
                mouse.PendingApply = false;
                mouse.LastApplyStatus = "Failed";
                mouse.LastApplyMessage = TruncateReason(reason);
                mouse.UpdatedUtc = now;
                var failureMessage = mouse.LastApplyMessage ?? "Mouse apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Mouse,
                    mouse.SettingsVersion,
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

    internal static bool IsMouseTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            MouseSettingsService.MouseModuleName,
            StringComparison.OrdinalIgnoreCase);

    private async Task<DeviceMouseSettings?> ResolveMouseSettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.MouseSettings is not null)
        {
            return device.MouseSettings;
        }

        var mouse = await _dbContext.DeviceMouseSettings
            .FirstOrDefaultAsync(k => k.DeviceId == device.Id, cancellationToken);
        if (mouse is not null)
        {
            device.MouseSettings = mouse;
        }

        return mouse;
    }
}
