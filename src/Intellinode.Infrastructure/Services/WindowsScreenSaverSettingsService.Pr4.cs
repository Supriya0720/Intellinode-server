using Intellinode.Application.Contracts.Admin;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed partial class WindowsScreenSaverSettingsService
{
    public async Task<WindowsScreenSaverQueueResult> TemplateQueueAsync(
        WindowsScreenSaverTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverQueueResult.Failure(
                    "ValidationFailed",
                    "Only QueueTemplate is supported on this endpoint.");
            }

            var queueResult = await QueueScreenSaverWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsScreenSaverModuleConstants.TemplateQueueFunctionName,
                "template",
                BuildTemplateApplyLogMessage(request.Execution),
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsScreenSaverQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Template queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverBulkResult> ExecuteNowBulkAsync(
        WindowsScreenSaverExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now bulk.");
            }

            var uniqueTargets = request.Targets
                .GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return await ExecuteNowForTargetsInternalAsync(
                uniqueTargets,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsScreenSaverExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsScreenSaverBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsScreenSaverSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsScreenSaverTargetRequest
                {
                    MacAddress = d.MacAddress,
                    OsType = ExtractOsType(d.MacAddress)
                })
                .ToList();

            return await ExecuteNowForTargetsInternalAsync(
                targets,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                cancellationToken,
                preloadedDevices: devices);
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private static string BuildTemplateApplyLogMessage(WindowsScreenSaverExecutionRequest execution)
    {
        var templateName = execution.TemplateName?.Trim();
        if (execution.TemplateId is > 0 && !string.IsNullOrWhiteSpace(templateName))
        {
            return $"Screen saver SysView template queue ({templateName}, id {execution.TemplateId.Value}).";
        }

        if (execution.TemplateId is > 0)
        {
            return $"Screen saver SysView template queue (id {execution.TemplateId.Value}).";
        }

        return "Screen saver SysView template queue.";
    }

    private bool ShouldReturnLegacySummary(WindowsScreenSaverOptionsRequest options) =>
        options.ReturnLegacySummary && _options.LegacySummaryEnabled;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private async Task<WindowsScreenSaverBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsScreenSaverTargetRequest> uniqueTargets,
        WindowsScreenSaverSettingsRequest settingsTemplate,
        WindowsScreenSaverExecutionRequest execution,
        WindowsScreenSaverOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken,
        List<Device>? preloadedDevices = null)
    {
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var batchTaskId = Guid.NewGuid();

        if (options.DryRun)
        {
            var dryRunMacs = uniqueTargets.Select(t => t.MacAddress.Trim()).ToList();
            var dryRunDevices = preloadedDevices is not null
                ? preloadedDevices.Where(d => dryRunMacs.Contains(d.MacAddress)).ToList()
                : await _dbContext.Devices
                    .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && dryRunMacs.Contains(d.MacAddress))
                    .ToListAsync(cancellationToken);
            var dryRunByMac = dryRunDevices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);

            var dryRunResults = new List<WindowsScreenSaverTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsScreenSaverTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsScreenSaverTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsScreenSaverTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsScreenSaverBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        var results = new List<WindowsScreenSaverTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!IsXpDevice(mac))
            {
                blocked++;
                results.Add(new WindowsScreenSaverTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var work = await QueueScreenSaverWorkAsync(
                target,
                settingsTemplate,
                execution,
                options,
                adminId,
                WindowsScreenSaverModuleConstants.InstantFunctionName,
                "instant",
                "Screen saver bulk instant apply queued.",
                cancellationToken);

            if (work.ExecuteNowResult is null)
            {
                blocked++;
                results.Add(new WindowsScreenSaverTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = work.ErrorCode ?? work.Message ?? "ApplyBlocked"
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty
                ? work.ExecuteNowResult.Response!.Data.TaskId
                : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsScreenSaverTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        return WindowsScreenSaverBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private static WindowsScreenSaverBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsScreenSaverTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsScreenSaverBulkData
            {
                TaskId = taskId,
                TotalTargets = totalTargets,
                Accepted = accepted,
                Blocked = blocked,
                Results = results,
                LegacySummary = includeLegacySummary ? BuildLegacySummary(accepted.ToString()) : null,
                CorrelationId = correlationId
            }
        };
}
