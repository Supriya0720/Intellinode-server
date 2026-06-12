using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsRegionalFormatSettingsService : IWindowsRegionalFormatSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsRegionalFormatPayloadBuilder _payloadBuilder;

    public WindowsRegionalFormatSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsRegionalFormatPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
    }

    public async Task<WindowsRegionalFormatExecuteNowResult> ExecuteNowAsync(
        WindowsRegionalFormatExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionalFormatExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueRegionalFormatWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsRegionalFormatModuleConstants.InstantFunctionName,
                "instant",
                "Regional format instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsRegionalFormatExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsRegionalFormatExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionalFormatQueueResult> QueueAsync(
        WindowsRegionalFormatQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionalFormatQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueRegionalFormatWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsRegionalFormatModuleConstants.QueuedFunctionName,
                "queued",
                "Regional format scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsRegionalFormatQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsRegionalFormatQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionalFormatCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsRegionalFormatCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsRegionalFormatCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsRegionalFormatSettings;
            var hasSettings = settings is not null;

            return WindowsRegionalFormatCurrentResult.Success(new WindowsRegionalFormatCurrentResponse
            {
                Success = true,
                Message = "Regional format settings fetched successfully.",
                Data = new WindowsRegionalFormatCurrentData
                {
                    Target = new WindowsRegionalFormatTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new WindowsRegionalFormatCurrentSettingsDto
                    {
                        TimeFormat = settings?.TimeFormat ?? string.Empty,
                        TimeSeparator = settings?.TimeSeparator ?? string.Empty,
                        AmSymbol = settings?.AmSymbol ?? string.Empty,
                        PmSymbol = settings?.PmSymbol ?? string.Empty,
                        ShortDateFormat = settings?.ShortDateFormat ?? string.Empty,
                        DateSeparator = settings?.DateSeparator ?? string.Empty,
                        LongDateFormat = settings?.LongDateFormat ?? string.Empty,
                        ShortDateSample = settings?.ShortDateSample ?? string.Empty,
                        LongDateSample = settings?.LongDateSample ?? string.Empty,
                        TimeSample = settings?.TimeSample,
                        AgentAction = settings?.AgentAction ?? 0,
                        SettingsVersion = settings?.SettingsVersion ?? 0,
                        PendingApply = settings?.PendingApply ?? false,
                        LastAppliedVersion = settings?.LastAppliedVersion,
                        LastAppliedUtc = settings?.LastAppliedUtc,
                        LastApplyStatus = settings?.LastApplyStatus,
                        LastApplyMessage = settings?.LastApplyMessage
                    },
                    Compat = new WindowsRegionalFormatCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsRegionalFormatCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionalFormatHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsRegionalFormatHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsRegionalFormatHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsRegionalFormatHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsRegionalFormatModuleConstants.ModuleName;

            var tasksQuery = _dbContext.DeviceTasks
                .AsNoTracking()
                .Where(t => t.DeviceId == device.Id && t.ModuleName == moduleName);

            if (query.FromUtc.HasValue)
            {
                tasksQuery = tasksQuery.Where(t => t.CreatedUtc >= query.FromUtc.Value);
            }

            if (query.ToUtc.HasValue)
            {
                tasksQuery = tasksQuery.Where(t => t.CreatedUtc <= query.ToUtc.Value);
            }

            var tasks = await tasksQuery.ToListAsync(cancellationToken);

            var logsQuery = _dbContext.DeviceSettingsApplyLogs
                .AsNoTracking()
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsRegionalFormat);

            if (query.FromUtc.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.CreatedUtc >= query.FromUtc.Value);
            }

            if (query.ToUtc.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.CreatedUtc <= query.ToUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                Enum.TryParse<SettingsApplyStatus>(statusFilter, true, out var parsedApplyStatus))
            {
                logsQuery = logsQuery.Where(l => l.Status == parsedApplyStatus);
            }

            var logs = await logsQuery.ToListAsync(cancellationToken);

            var taskItems = tasks.Select(t => new WindowsRegionalFormatHistoryItem
            {
                TaskId = t.Id,
                LegacyTaskId = t.LegacyTaskId,
                ModuleName = t.ModuleName,
                FunctionName = t.FunctionName,
                TaskStatus = t.Status.ToString(),
                ApplyStatus = MapTaskToApplyStatus(t.Status),
                ApplyMode = MapTaskApplyMode(t.FunctionName),
                CreatedUtc = t.CreatedUtc
            });

            var logItems = logs.Select(l => new WindowsRegionalFormatHistoryItem
            {
                TaskId = l.TaskId,
                LegacyTaskId = l.LegacyTaskId,
                SettingsVersion = l.SettingsVersion,
                ApplyStatus = l.Status.ToString(),
                ApplyMode = l.ApplyMode,
                Message = l.Message,
                CreatedUtc = l.CreatedUtc
            });

            var merged = taskItems
                .Concat(logItems)
                .OrderByDescending(i => i.CreatedUtc);

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                merged = merged
                    .Where(i => string.Equals(i.ApplyStatus, statusFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(i => i.CreatedUtc);
            }

            var totalCount = merged.Count();
            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
            var items = merged
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return WindowsRegionalFormatHistoryResult.Success(new WindowsRegionalFormatHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsRegionalFormatHistoryData
                {
                    Target = new WindowsRegionalFormatTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new WindowsRegionalFormatPagination
                    {
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = totalCount
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsRegionalFormatHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionalFormatBulkResult> ExecuteNowBulkAsync(
        WindowsRegionalFormatExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionalFormatBulkResult.Failure(
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
            return WindowsRegionalFormatBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionalFormatBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsRegionalFormatExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionalFormatBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsRegionalFormatBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsRegionalFormatSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsRegionalFormatTargetRequest
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
            return WindowsRegionalFormatBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsRegionalFormatSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetRegionalFormatBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return "EnrollmentStateBlocked";
        }

        var moduleName = _payloadBuilder.GetModuleName();
        var hasPendingTask = await _dbContext.DeviceTasks
            .AnyAsync(
                t => t.DeviceId == deviceId &&
                     t.ModuleName == moduleName &&
                     (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess),
                cancellationToken);

        return hasPendingTask ? "PendingTaskExists" : null;
    }

    private async Task<int> GetNextLegacyTaskIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var maxLegacyId = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId)
            .MaxAsync(t => (int?)t.LegacyTaskId, cancellationToken);

        return (maxLegacyId ?? 0) + 1;
    }

    internal async Task<RegionalFormatWorkResult> QueueRegionalFormatWorkAsync(
        WindowsRegionalFormatTargetRequest target,
        WindowsRegionalFormatSettingsRequest settings,
        WindowsRegionalFormatExecutionRequest execution,
        WindowsRegionalFormatOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var normalizedMac = target.MacAddress.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;
        var agentAction = ParseAgentAction(execution.AgentAction);

        if (options.DryRun)
        {
            if (string.Equals(functionName, WindowsRegionalFormatModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return RegionalFormatWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return RegionalFormatWorkResult.FromExecuteNow(BuildExecuteNowResponse(
                target,
                execution,
                Guid.Empty,
                now,
                options.ReturnLegacySummary,
                correlationId));
        }

        var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
        if (device is null)
        {
            return RegionalFormatWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return RegionalFormatWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
        }

        var queueAttempt = await TryQueueForDeviceAsync(
            device,
            settings,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            agentAction,
            cancellationToken);

        if (!queueAttempt.Success)
        {
            return queueAttempt.Reason switch
            {
                "PayloadTooLarge" => RegionalFormatWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsRegionalFormatPayloadBuilder.MaxFunctionParameterLength} characters."),
                _ => RegionalFormatWorkResult.Failure("ApplyBlocked", queueAttempt.Reason ?? "ApplyBlocked")
            };
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (string.Equals(functionName, WindowsRegionalFormatModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return RegionalFormatWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return RegionalFormatWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<WindowsRegionalFormatBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsRegionalFormatTargetRequest> uniqueTargets,
        WindowsRegionalFormatSettingsRequest settingsTemplate,
        WindowsRegionalFormatExecutionRequest execution,
        WindowsRegionalFormatOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken,
        List<Device>? preloadedDevices = null)
    {
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var batchTaskId = Guid.NewGuid();
        var agentAction = ParseAgentAction(execution.AgentAction);

        if (options.DryRun)
        {
            var dryRunMacs = uniqueTargets.Select(t => t.MacAddress.Trim()).ToList();
            var dryRunDevices = preloadedDevices is not null
                ? preloadedDevices.Where(d => dryRunMacs.Contains(d.MacAddress)).ToList()
                : await _dbContext.Devices
                    .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && dryRunMacs.Contains(d.MacAddress))
                    .ToListAsync(cancellationToken);
            var dryRunByMac = dryRunDevices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);

            var dryRunResults = new List<WindowsRegionalFormatTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsRegionalFormatTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsRegionalFormatTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsRegionalFormatTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsRegionalFormatBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                options.ReturnLegacySummary,
                correlationId));
        }

        Dictionary<string, Device> byMac;
        if (preloadedDevices is not null)
        {
            byMac = preloadedDevices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var macs = uniqueTargets.Select(t => t.MacAddress.Trim()).ToList();
            var devices = await _dbContext.Devices
                .Include(d => d.WindowsRegionalFormatSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsRegionalFormatTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsRegionalFormatTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "DeviceNotFound"
                });
                continue;
            }

            if (!IsXpDevice(device.MacAddress))
            {
                blocked++;
                results.Add(new WindowsRegionalFormatTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var queueAttempt = await TryQueueForDeviceAsync(
                device,
                settingsTemplate,
                adminId,
                WindowsRegionalFormatModuleConstants.InstantFunctionName,
                "instant",
                "Regional format bulk instant apply queued.",
                agentAction,
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsRegionalFormatTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsRegionalFormatTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsRegionalFormatBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason)> TryQueueForDeviceAsync(
        Device device,
        WindowsRegionalFormatSettingsRequest settingsTemplate,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        int agentAction,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            return (false, null, "EnrollmentStateBlocked");
        }

        var settings = CloneSettings(settingsTemplate);

        var blockReason = await GetRegionalFormatBlockReasonAsync(
            device.Id,
            device.EnrollmentState,
            cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason);
        }

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var functionPayload = BuildFunctionPayload(settings, legacyTaskId, agentAction);
        if (functionPayload.Length > WindowsRegionalFormatPayloadBuilder.MaxFunctionParameterLength)
        {
            return (false, null, "PayloadTooLarge");
        }

        var now = DateTime.UtcNow;
        var regionalFormatSettings = device.WindowsRegionalFormatSettings;
        if (regionalFormatSettings is null)
        {
            regionalFormatSettings = new DeviceWindowsRegionalFormatSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsRegionalFormatSettings.Add(regionalFormatSettings);
            device.WindowsRegionalFormatSettings = regionalFormatSettings;
        }

        MapSettingsToEntity(settings, regionalFormatSettings, agentAction, adminId, now);

        var moduleName = _payloadBuilder.GetModuleName();
        var signalSuffix = _payloadBuilder.GetSignalSuffix();
        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = moduleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = $"{device.MacAddress.Trim()}&{signalSuffix}",
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.WindowsRegionalFormat,
            regionalFormatSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        return (true, task, null);
    }

    private string BuildFunctionPayload(
        WindowsRegionalFormatSettingsRequest settings,
        int legacyTaskId,
        int agentAction) =>
        _payloadBuilder.BuildPayload(new WindowsRegionalFormatPayloadRequest
        {
            TimeFormat = settings.TimeFormat.Trim(),
            TimeSeparator = settings.TimeSeparator.Trim(),
            AmSymbol = settings.AmSymbol.Trim(),
            PmSymbol = settings.PmSymbol.Trim(),
            ShortDateFormat = settings.ShortDateFormat.Trim(),
            DateSeparator = settings.DateSeparator.Trim(),
            LongDateFormat = settings.LongDateFormat.Trim(),
            ShortDateSample = settings.ShortDateSample.Trim(),
            LongDateSample = settings.LongDateSample.Trim(),
            TaskID = legacyTaskId,
            AgentAction = agentAction
        });

    private static void MapSettingsToEntity(
        WindowsRegionalFormatSettingsRequest settings,
        DeviceWindowsRegionalFormatSettings entity,
        int agentAction,
        Guid? adminId,
        DateTime now)
    {
        entity.TimeFormat = settings.TimeFormat.Trim();
        entity.TimeSeparator = settings.TimeSeparator.Trim();
        entity.AmSymbol = settings.AmSymbol.Trim();
        entity.PmSymbol = settings.PmSymbol.Trim();
        entity.ShortDateFormat = settings.ShortDateFormat.Trim();
        entity.DateSeparator = settings.DateSeparator.Trim();
        entity.LongDateFormat = settings.LongDateFormat.Trim();
        entity.ShortDateSample = settings.ShortDateSample.Trim();
        entity.LongDateSample = settings.LongDateSample.Trim();
        entity.TimeSample = string.IsNullOrWhiteSpace(settings.TimeSample) ? null : settings.TimeSample.Trim();
        entity.AgentAction = agentAction;
        entity.SettingsVersion++;
        entity.PendingApply = true;
        entity.UpdatedBy = adminId;
        entity.UpdatedUtc = now;
    }

    private static WindowsRegionalFormatSettingsRequest CloneSettings(WindowsRegionalFormatSettingsRequest source) =>
        new()
        {
            TimeFormat = source.TimeFormat,
            TimeSeparator = source.TimeSeparator,
            AmSymbol = source.AmSymbol,
            PmSymbol = source.PmSymbol,
            ShortDateFormat = source.ShortDateFormat,
            DateSeparator = source.DateSeparator,
            LongDateFormat = source.LongDateFormat,
            ShortDateSample = source.ShortDateSample,
            LongDateSample = source.LongDateSample,
            TimeSample = source.TimeSample
        };

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsRegionalFormatModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsRegionalFormatModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "queued";
        }

        return "queued";
    }

    private static string NormalizeScheduleType(string? scheduleType)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
        {
            return "InstantApply";
        }

        return scheduleType.Trim();
    }

    internal sealed class RegionalFormatWorkResult
    {
        public WindowsRegionalFormatExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsRegionalFormatQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static RegionalFormatWorkResult FromExecuteNow(WindowsRegionalFormatExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsRegionalFormatExecuteNowResult.Success(response) };

        public static RegionalFormatWorkResult FromQueue(WindowsRegionalFormatQueueResponse response) =>
            new() { QueueResult = WindowsRegionalFormatQueueResult.Success(response) };

        public static RegionalFormatWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }

    private static string MapTaskToApplyStatus(DeviceTaskStatus status) => status switch
    {
        DeviceTaskStatus.Pending => "Pending",
        DeviceTaskStatus.InProcess => "Delivered",
        DeviceTaskStatus.Completed => "Applied",
        DeviceTaskStatus.Failed => "Failed",
        _ => "Pending"
    };

    private static string ExtractOsType(string macAddress)
    {
        var suffix = ExtractOsSuffix(macAddress);
        return suffix ?? "XP";
    }

    private static string? ExtractOsSuffix(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return null;
        }

        var trimmed = macAddress.Trim();
        var idx = trimmed.LastIndexOf(':');
        if (idx < 0 || idx == trimmed.Length - 1)
        {
            return null;
        }

        return trimmed[(idx + 1)..].ToUpperInvariant();
    }

    private static WindowsRegionalFormatLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsRegionalFormatQueueResponse BuildQueueResponse(
        WindowsRegionalFormatTargetRequest target,
        WindowsRegionalFormatExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new WindowsRegionalFormatQueueData
            {
                TaskId = taskId,
                Target = new WindowsRegionalFormatTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsRegionalFormatExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsRegionalFormatExecuteNowResponse BuildExecuteNowResponse(
        WindowsRegionalFormatTargetRequest target,
        WindowsRegionalFormatExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsRegionalFormatExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsRegionalFormatTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsRegionalFormatExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsRegionalFormatBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsRegionalFormatTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsRegionalFormatBulkData
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
