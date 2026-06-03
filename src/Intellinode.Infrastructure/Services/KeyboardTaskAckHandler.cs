using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class KeyboardTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<KeyboardTaskAckHandler> _logger;

    public KeyboardTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<KeyboardTaskAckHandler> logger)
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
        if (!IsKeyboardTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var keyboard = await ResolveKeyboardSettingsAsync(device, cancellationToken);
        if (keyboard is null)
        {
            _logger.LogWarning(
                "Keyboard task {TaskId} ack ignored: no device_keyboard_settings row for device {DeviceId}",
                task.Id,
                device.Id);
            return;
        }

        var now = DateTime.UtcNow;
        switch (terminalStatus)
        {
            case DeviceTaskStatus.Completed:
                keyboard.LastAppliedVersion = keyboard.SettingsVersion;
                keyboard.LastAppliedUtc = now;
                keyboard.PendingApply = false;
                keyboard.LastApplyStatus = "Applied";
                keyboard.LastApplyMessage = null;
                keyboard.UpdatedUtc = now;
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Keyboard,
                    keyboard.SettingsVersion,
                    "instant",
                    SettingsApplyStatus.Applied,
                    adminId: null,
                    message: null,
                    cancellationToken,
                    task.Id,
                    task.LegacyTaskId);
                break;

            case DeviceTaskStatus.Failed:
                keyboard.PendingApply = false;
                keyboard.LastApplyStatus = "Failed";
                keyboard.LastApplyMessage = TruncateReason(reason);
                keyboard.UpdatedUtc = now;
                var failureMessage = keyboard.LastApplyMessage ?? "Keyboard apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Keyboard,
                    keyboard.SettingsVersion,
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

    internal static bool IsKeyboardTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            KeyboardSettingsService.KeyboardModuleName,
            StringComparison.OrdinalIgnoreCase);

    private async Task<DeviceKeyboardSettings?> ResolveKeyboardSettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.KeyboardSettings is not null)
        {
            return device.KeyboardSettings;
        }

        var keyboard = await _dbContext.DeviceKeyboardSettings
            .FirstOrDefaultAsync(k => k.DeviceId == device.Id, cancellationToken);
        if (keyboard is not null)
        {
            device.KeyboardSettings = keyboard;
        }

        return keyboard;
    }
}
