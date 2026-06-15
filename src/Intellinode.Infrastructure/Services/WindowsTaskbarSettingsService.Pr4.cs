using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Contracts.Agents;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed partial class WindowsTaskbarSettingsService
{
    public async Task<WindowsTaskbarLiveResult> GetLiveAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_options.AgentLiveReadEnabled)
            {
                return WindowsTaskbarLiveResult.Failure(
                    "FeatureDisabled",
                    "Taskbar agent live read is disabled.");
            }

            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsTaskbarLiveResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacWithLiveAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsTaskbarLiveResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var live = device.WindowsTaskbarLiveSettings;
            return WindowsTaskbarLiveResult.Success(new WindowsTaskbarLiveResponse
            {
                Success = true,
                Message = live is null
                    ? "No agent-reported taskbar state is available yet."
                    : "Agent-reported taskbar state fetched successfully.",
                Data = new WindowsTaskbarLiveData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Settings = live is null ? null : MapLiveSettingsDto(live),
                    Compat = new WindowsTaskbarCurrentCompatDto
                    {
                        Source = live is null ? "none" : "agent"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsTaskbarLiveResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsTaskbarRefreshLiveResult> RefreshLiveAsync(
        string macAddress,
        WindowsTaskbarRefreshLiveOptionsRequest? options = null,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_options.Enabled || _options.ReadOnly)
            {
                return WindowsTaskbarRefreshLiveResult.Failure(
                    "FeatureDisabled",
                    "Taskbar endpoint is disabled or read-only.");
            }

            if (!_options.AgentLiveReadEnabled)
            {
                return WindowsTaskbarRefreshLiveResult.Failure(
                    "FeatureDisabled",
                    "Taskbar agent live read is disabled.");
            }

            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsTaskbarRefreshLiveResult.Failure("ValidationFailed", "macAddress is required.");
            }

            if (!IsXpDevice(normalizedMac))
            {
                return WindowsTaskbarRefreshLiveResult.Failure(
                    "ValidationFailed",
                    "Live read is supported for Windows XP targets only.");
            }

            var correlationId = options?.CorrelationId ?? Guid.NewGuid();
            var now = DateTime.UtcNow;

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsTaskbarRefreshLiveResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
            {
                return WindowsTaskbarRefreshLiveResult.Failure("ApplyBlocked", "EnrollmentStateBlocked");
            }

            var blockReason = await GetLiveReadBlockReasonAsync(device.Id, cancellationToken);
            if (blockReason is not null)
            {
                return WindowsTaskbarRefreshLiveResult.Failure("ApplyBlocked", blockReason);
            }

            var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
            var task = new DeviceTask
            {
                DeviceId = device.Id,
                LegacyTaskId = legacyTaskId,
                ModuleName = WindowsTaskbarModuleConstants.ModuleName,
                FunctionName = WindowsTaskbarModuleConstants.LiveReadFunctionName,
                FunctionParameter = "{}",
                ExtraData = _payloadBuilder.BuildExtraData(
                    device.MacAddress,
                    string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
                        ? WindowsTaskbarModuleConstants.DefaultSignalSuffix
                        : _options.DefaultSignalSuffix),
                Status = DeviceTaskStatus.Pending,
                CreatedUtc = now
            };
            _dbContext.DeviceTasks.Add(task);

            await _resolver.WriteApplyLogAsync(
                device.Id,
                SettingsKind.WindowsTaskbar,
                version: 0,
                applyMode: "live-read",
                SettingsApplyStatus.Pending,
                adminId,
                "Taskbar live read queued.",
                cancellationToken,
                task.Id,
                legacyTaskId);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return WindowsTaskbarRefreshLiveResult.Success(new WindowsTaskbarRefreshLiveResponse
            {
                Success = true,
                Message = "Taskbar live read queued.",
                Data = new WindowsTaskbarRefreshLiveData
                {
                    TaskId = task.Id,
                    Target = BuildTargetResponse(device.MacAddress),
                    Execution = new WindowsTaskbarExecutionResponse
                    {
                        ScheduleType = "LiveRead",
                        Status = "Pending",
                        QueuedAtUtc = task.CreatedUtc
                    },
                    CorrelationId = correlationId
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsTaskbarRefreshLiveResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<AgentTaskbarLiveReportResult> ReportAgentLiveAsync(
        Guid deviceId,
        AgentTaskbarLiveReportRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_options.AgentLiveReadEnabled)
            {
                return AgentTaskbarLiveReportResult.Failure(
                    "FeatureDisabled",
                    "Taskbar agent live read is disabled.");
            }

            if (!WindowsTaskbarLivePayloadParser.TryParse(request, out var parsed))
            {
                return AgentTaskbarLiveReportResult.Failure(
                    "ValidationFailed",
                    "Taskbar live report must include flat settings or WinCELinux.XPTaskbarProperties.");
            }

            var device = await _dbContext.Devices
                .Include(d => d.WindowsTaskbarLiveSettings)
                .FirstOrDefaultAsync(
                    d => d.Id == deviceId && d.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (device is null)
            {
                return AgentTaskbarLiveReportResult.Failure(
                    "DeviceNotFound",
                    "Device associated with this token was not found.");
            }

            var now = DateTime.UtcNow;
            var live = device.WindowsTaskbarLiveSettings;
            if (live is null)
            {
                live = new DeviceWindowsTaskbarLiveSettings
                {
                    DeviceId = device.Id,
                    ReportVersion = 0,
                    CreatedUtc = now,
                    UpdatedUtc = now
                };
                _dbContext.DeviceWindowsTaskbarLiveSettings.Add(live);
                device.WindowsTaskbarLiveSettings = live;
            }

            live.LockTaskbar = parsed.LockTaskbar;
            live.AutoHideTaskbar = parsed.AutoHideTaskbar;
            live.KeepTaskbarOnTop = parsed.KeepTaskbarOnTop;
            live.GroupSimilarButtons = parsed.GroupSimilarButtons;
            live.ShowQuickLaunch = parsed.ShowQuickLaunch;
            live.ShowClock = parsed.ShowClock;
            live.HideInactiveIcons = parsed.HideInactiveIcons;
            live.ReportVersion++;
            live.CollectedUtc = now;
            live.UpdatedUtc = now;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return AgentTaskbarLiveReportResult.Success(new AgentTaskbarLiveReportResponse
            {
                Success = true,
                Message = "Taskbar live state stored.",
                ReportVersion = live.ReportVersion,
                CollectedUtc = live.CollectedUtc
            });
        }
        catch (Exception ex)
        {
            return AgentTaskbarLiveReportResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private static WindowsTaskbarLiveSettingsDto MapLiveSettingsDto(DeviceWindowsTaskbarLiveSettings live) =>
        new()
        {
            LockTaskbar = live.LockTaskbar,
            AutoHideTaskbar = live.AutoHideTaskbar,
            KeepTaskbarOnTop = live.KeepTaskbarOnTop,
            GroupSimilarButtons = live.GroupSimilarButtons,
            ShowQuickLaunch = live.ShowQuickLaunch,
            ShowClock = live.ShowClock,
            HideInactiveIcons = live.HideInactiveIcons,
            ReportVersion = live.ReportVersion,
            CollectedUtc = live.CollectedUtc
        };

    private async Task<Device?> FindDeviceByMacWithLiveAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsTaskbarLiveSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetLiveReadBlockReasonAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var hasPendingLiveRead = await _dbContext.DeviceTasks
            .AnyAsync(
                t => t.DeviceId == deviceId &&
                     t.ModuleName == WindowsTaskbarModuleConstants.ModuleName &&
                     t.FunctionName == WindowsTaskbarModuleConstants.LiveReadFunctionName &&
                     (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess),
                cancellationToken);

        return hasPendingLiveRead ? "PendingLiveReadTaskExists" : null;
    }
}
