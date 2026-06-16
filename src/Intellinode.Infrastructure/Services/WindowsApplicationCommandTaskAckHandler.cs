using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsApplicationCommandTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly ILogger<WindowsApplicationCommandTaskAckHandler> _logger;

    public WindowsApplicationCommandTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        ILogger<WindowsApplicationCommandTaskAckHandler> logger)
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
        if (!IsApplicationCommandTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        var settings = await ResolveSettingsAsync(device, cancellationToken);
        if (settings is null)
        {
            _logger.LogWarning(
                "Application command task {TaskId} ack ignored: no device_windows_application_command_settings row for device {DeviceId}",
                task.Id,
                device.Id);
            return;
        }

        var applyMode = MapTaskApplyMode(task.FunctionName);
        var settingsKind = ResolveSettingsKind(task.ModuleName);
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
                    settingsKind,
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
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    settingsKind,
                    settings.SettingsVersion,
                    applyMode,
                    SettingsApplyStatus.Failed,
                    adminId: null,
                    message: settings.LastApplyMessage ?? "Application command apply failed.",
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

    internal static bool IsApplicationCommandTask(DeviceTask task) =>
        string.Equals(task.ModuleName, WindowsApplicationCommandModuleConstants.ApplicationModuleName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(task.ModuleName, WindowsApplicationCommandModuleConstants.CommandModuleName, StringComparison.OrdinalIgnoreCase);

    internal static SettingsKind ResolveSettingsKind(string? moduleName) =>
        string.Equals(moduleName, WindowsApplicationCommandModuleConstants.CommandModuleName, StringComparison.OrdinalIgnoreCase)
            ? SettingsKind.WindowsCommand
            : SettingsKind.WindowsApplication;

    private static string MapTaskApplyMode(string functionName) =>
        WindowsApplicationCommandModuleConstants.MapApplyMode(functionName);

    private async Task<DeviceWindowsApplicationCommandSettings?> ResolveSettingsAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        if (device.WindowsApplicationCommandSettings is not null)
        {
            return device.WindowsApplicationCommandSettings;
        }

        var settings = await _dbContext.DeviceWindowsApplicationCommandSettings
            .FirstOrDefaultAsync(s => s.DeviceId == device.Id, cancellationToken);
        if (settings is not null)
        {
            device.WindowsApplicationCommandSettings = settings;
        }

        return settings;
    }
}
