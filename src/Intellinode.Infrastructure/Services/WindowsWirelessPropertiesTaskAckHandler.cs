using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsWirelessPropertiesTaskAckHandler
{
    public const int MaxReasonLength = 500;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsWirelessPropertiesPayloadBuilder _payloadBuilder;
    private readonly ILogger<WindowsWirelessPropertiesTaskAckHandler> _logger;

    public WindowsWirelessPropertiesTaskAckHandler(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsWirelessPropertiesPayloadBuilder payloadBuilder,
        ILogger<WindowsWirelessPropertiesTaskAckHandler> logger)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _logger = logger;
    }

    public async Task ApplyAckAsync(
        Device device,
        DeviceTask task,
        DeviceTaskStatus terminalStatus,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!IsWirelessPropertiesTask(task))
        {
            return;
        }

        if (terminalStatus is not DeviceTaskStatus.Completed and not DeviceTaskStatus.Failed)
        {
            return;
        }

        if (!_payloadBuilder.TryParseCompactTaskReference(
                task.FunctionParameter,
                out var settingsVersion,
                out var profileKey))
        {
            _logger.LogWarning(
                "Wireless Network Security task {TaskId} ack ignored: invalid compact functionParameter for device {DeviceId}",
                task.Id,
                device.Id);
            return;
        }

        var profile = await _dbContext.DeviceWindowsWirelessProfileSettings
            .FirstOrDefaultAsync(
                p => p.DeviceId == device.Id && p.ProfileKey == profileKey,
                cancellationToken);

        if (profile is null)
        {
            _logger.LogWarning(
                "Wireless Network Security task {TaskId} ack ignored: no profile row for device {DeviceId}, profileKey {ProfileKey}",
                task.Id,
                device.Id,
                profileKey);
            return;
        }

        var snapshot = await _dbContext.DeviceWindowsWirelessProfileSettingsSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DeviceId == device.Id &&
                     s.ProfileKey == profileKey &&
                     s.SettingsVersion == settingsVersion,
                cancellationToken);

        var queuedSettingsJson = snapshot?.SettingsJson ?? profile.SettingsJson;
        var isDeleteTask = WindowsWirelessPropertiesPayloadShape.IsDeleteShapeInnerSettingsJson(queuedSettingsJson);
        var applyMode = MapTaskApplyMode(task.FunctionName);
        var now = DateTime.UtcNow;

        switch (terminalStatus)
        {
            case DeviceTaskStatus.Completed:
                if (isDeleteTask)
                {
                    _dbContext.DeviceWindowsWirelessProfileSettings.Remove(profile);
                    await _resolver.WriteApplyLogAsync(
                        device.Id,
                        SettingsKind.WindowsWirelessProperties,
                        settingsVersion,
                        applyMode,
                        SettingsApplyStatus.Applied,
                        adminId: null,
                        message: $"Wireless profile '{profile.Ssid}' deleted.",
                        cancellationToken,
                        task.Id,
                        task.LegacyTaskId);
                }
                else
                {
                    profile.LastAppliedVersion = settingsVersion;
                    profile.LastAppliedUtc = now;
                    profile.PendingApply = false;
                    profile.LastApplyStatus = "Applied";
                    profile.LastApplyMessage = null;
                    profile.UpdatedUtc = now;
                    await _resolver.WriteApplyLogAsync(
                        device.Id,
                        SettingsKind.WindowsWirelessProperties,
                        settingsVersion,
                        applyMode,
                        SettingsApplyStatus.Applied,
                        adminId: null,
                        message: null,
                        cancellationToken,
                        task.Id,
                        task.LegacyTaskId);
                }

                break;

            case DeviceTaskStatus.Failed:
                profile.PendingApply = false;
                profile.LastApplyStatus = "Failed";
                profile.LastApplyMessage = TruncateReason(reason);
                profile.UpdatedUtc = now;
                var failureMessage = profile.LastApplyMessage ?? "Wireless profile apply failed.";
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.WindowsWirelessProperties,
                    settingsVersion,
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

    public static bool IsWirelessPropertiesTask(DeviceTask task) =>
        string.Equals(
            task.ModuleName,
            WindowsWirelessPropertiesModuleConstants.ModuleName,
            StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsWirelessPropertiesModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsWirelessPropertiesModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "queued";
        }

        return "queued";
    }
}
