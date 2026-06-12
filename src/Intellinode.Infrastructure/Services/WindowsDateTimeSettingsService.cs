using System.Globalization;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsDateTimeSettingsService : IWindowsDateTimeSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsDateTimePayloadBuilder _payloadBuilder;

    public WindowsDateTimeSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsDateTimePayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
    }

    public async Task<WindowsDateTimeExecuteNowResult> ExecuteNowAsync(
        WindowsDateTimeExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsDateTimeExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueDateTimeWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsDateTimeModuleConstants.InstantFunctionName,
                "instant",
                "Date/time instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsDateTimeExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsDateTimeExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsDateTimeQueueResult> QueueAsync(
        WindowsDateTimeQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsDateTimeQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueDateTimeWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsDateTimeModuleConstants.QueuedFunctionName,
                "queued",
                "Date/time scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsDateTimeQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsDateTimeQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsDateTimeCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsDateTimeCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsDateTimeCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsDateTimeSettings;
            var hasSettings = settings is not null;

            return WindowsDateTimeCurrentResult.Success(new WindowsDateTimeCurrentResponse
            {
                Success = true,
                Message = "Date/time settings fetched successfully.",
                Data = new WindowsDateTimeCurrentData
                {
                    Target = new WindowsDateTimeTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new WindowsDateTimeCurrentSettingsDto
                    {
                        ApplyMode = settings?.ApplyMode ?? default,
                        CurrentDateLocal = settings?.CurrentDateLocal,
                        CurrentTimeLocal = FormatTimeLocal(settings?.CurrentTimeLocal),
                        TimeZoneDisplay = settings?.TimeZoneDisplay,
                        WindowsTzKey = settings?.WindowsTzKey,
                        TimeServer = settings?.TimeServer,
                        AgentAction = settings?.AgentAction ?? 0,
                        SettingsVersion = settings?.SettingsVersion ?? 0,
                        PendingApply = settings?.PendingApply ?? false,
                        LastAppliedVersion = settings?.LastAppliedVersion,
                        LastAppliedUtc = settings?.LastAppliedUtc,
                        LastApplyStatus = settings?.LastApplyStatus,
                        LastApplyMessage = settings?.LastApplyMessage
                    },
                    Compat = new WindowsDateTimeCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsDateTimeCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsDateTimeHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsDateTimeHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsDateTimeHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsDateTimeHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleNames = new[]
            {
                WindowsDateTimeModuleConstants.DateTimeModuleName,
                WindowsDateTimeModuleConstants.TimeZoneModuleName,
                WindowsDateTimeModuleConstants.TimeServerModuleName
            };

            var tasksQuery = _dbContext.DeviceTasks
                .AsNoTracking()
                .Where(t => t.DeviceId == device.Id && moduleNames.Contains(t.ModuleName));

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsDateTimeSetup);

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

            var taskItems = tasks.Select(t => new WindowsDateTimeHistoryItem
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

            var logItems = logs.Select(l => new WindowsDateTimeHistoryItem
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

            return WindowsDateTimeHistoryResult.Success(new WindowsDateTimeHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsDateTimeHistoryData
                {
                    Target = new WindowsDateTimeTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new WindowsDateTimePagination
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
            return WindowsDateTimeHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsDateTimeBulkResult> ExecuteNowBulkAsync(
        WindowsDateTimeExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsDateTimeBulkResult.Failure(
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
            return WindowsDateTimeBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsDateTimeBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsDateTimeExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsDateTimeBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsDateTimeBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsDateTimeSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsDateTimeTargetRequest
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
            return WindowsDateTimeBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsDateTimeSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetDateTimeBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        WindowsDateTimeApplyMode applyMode,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return "EnrollmentStateBlocked";
        }

        var moduleName = _payloadBuilder.GetModuleNameForApplyMode(applyMode);
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

    internal async Task<DateTimeWorkResult> QueueDateTimeWorkAsync(
        WindowsDateTimeTargetRequest target,
        WindowsDateTimeSettingsRequest settings,
        WindowsDateTimeExecutionRequest execution,
        WindowsDateTimeOptionsRequest options,
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
            if (string.Equals(functionName, WindowsDateTimeModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return DateTimeWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return DateTimeWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return DateTimeWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return DateTimeWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
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
                "PayloadTooLarge" => DateTimeWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsDateTimePayloadBuilder.MaxFunctionParameterLength} characters."),
                "InvalidTimeZone" => DateTimeWorkResult.Failure(
                    "ValidationFailed",
                    "Invalid time zone."),
                _ => DateTimeWorkResult.Failure("ApplyBlocked", queueAttempt.Reason ?? "ApplyBlocked")
            };
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (string.Equals(functionName, WindowsDateTimeModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return DateTimeWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return DateTimeWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<WindowsDateTimeBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsDateTimeTargetRequest> uniqueTargets,
        WindowsDateTimeSettingsRequest settingsTemplate,
        WindowsDateTimeExecutionRequest execution,
        WindowsDateTimeOptionsRequest options,
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

            var dryRunResults = new List<WindowsDateTimeTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsDateTimeTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsDateTimeTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsDateTimeTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsDateTimeBulkResult.Success(BuildBulkResponse(
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
                .Include(d => d.WindowsDateTimeSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsDateTimeTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsDateTimeTargetResult
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
                results.Add(new WindowsDateTimeTargetResult
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
                WindowsDateTimeModuleConstants.InstantFunctionName,
                "instant",
                "Date/time bulk instant apply queued.",
                agentAction,
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsDateTimeTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsDateTimeTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsDateTimeBulkResult.Success(BuildBulkResponse(
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
        WindowsDateTimeSettingsRequest settingsTemplate,
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

        var blockReason = await GetDateTimeBlockReasonAsync(
            device.Id,
            device.EnrollmentState,
            settings.ApplyMode,
            cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason);
        }

        if (settings.ApplyMode == WindowsDateTimeApplyMode.TimeZone)
        {
            var displayName = settings.TimeZoneDisplay?.Trim() ?? string.Empty;
            var windowsTzKey = settings.WindowsTzKey?.Trim() ?? string.Empty;
            var isValidTimeZone = await _dbContext.WindowsTimeZoneMasters
                .AnyAsync(
                    tz => tz.IsActive &&
                          tz.DisplayName == displayName &&
                          tz.WindowsTzKey == windowsTzKey,
                    cancellationToken);
            if (!isValidTimeZone)
            {
                return (false, null, "InvalidTimeZone");
            }
        }

        var parsedTime = ParseTimeLocal(settings.CurrentTimeLocal);
        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var functionPayload = BuildFunctionPayload(settings, legacyTaskId, agentAction, parsedTime);
        if (functionPayload.Length > WindowsDateTimePayloadBuilder.MaxFunctionParameterLength)
        {
            return (false, null, "PayloadTooLarge");
        }

        var now = DateTime.UtcNow;
        var dateTimeSettings = device.WindowsDateTimeSettings;
        if (dateTimeSettings is null)
        {
            dateTimeSettings = new DeviceWindowsDateTimeSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsDateTimeSettings.Add(dateTimeSettings);
            device.WindowsDateTimeSettings = dateTimeSettings;
        }

        MapSettingsToEntity(settings, dateTimeSettings, agentAction, parsedTime, adminId, now);

        var signalSuffix = _payloadBuilder.GetSignalSuffixForApplyMode(settings.ApplyMode);
        var moduleName = _payloadBuilder.GetModuleNameForApplyMode(settings.ApplyMode);
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
            SettingsKind.WindowsDateTimeSetup,
            dateTimeSettings.SettingsVersion,
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
        WindowsDateTimeSettingsRequest settings,
        int legacyTaskId,
        int agentAction,
        TimeOnly? parsedTime) =>
        _payloadBuilder.BuildPayload(new WindowsDateTimePayloadRequest
        {
            ApplyMode = settings.ApplyMode,
            CurrentDateLocal = settings.CurrentDateLocal,
            CurrentTimeLocal = parsedTime,
            TimeZoneDisplay = settings.TimeZoneDisplay?.Trim() ?? string.Empty,
            WindowsTzKey = settings.WindowsTzKey?.Trim() ?? string.Empty,
            TimeServer = settings.TimeServer?.Trim() ?? string.Empty,
            TaskID = legacyTaskId,
            AgentAction = agentAction
        });

    private static void MapSettingsToEntity(
        WindowsDateTimeSettingsRequest settings,
        DeviceWindowsDateTimeSettings entity,
        int agentAction,
        TimeOnly? parsedTime,
        Guid? adminId,
        DateTime now)
    {
        entity.ApplyMode = settings.ApplyMode;
        entity.CurrentDateLocal = settings.CurrentDateLocal;
        entity.CurrentTimeLocal = parsedTime;
        entity.TimeZoneDisplay = settings.TimeZoneDisplay?.Trim();
        entity.WindowsTzKey = settings.WindowsTzKey?.Trim();
        entity.TimeServer = settings.TimeServer?.Trim();
        entity.AgentAction = agentAction;
        entity.SettingsVersion++;
        entity.PendingApply = true;
        entity.UpdatedBy = adminId;
        entity.UpdatedUtc = now;
    }

    private static WindowsDateTimeSettingsRequest CloneSettings(WindowsDateTimeSettingsRequest source) =>
        new()
        {
            ApplyMode = source.ApplyMode,
            CurrentDateLocal = source.CurrentDateLocal,
            CurrentTimeLocal = source.CurrentTimeLocal,
            TimeZoneDisplay = source.TimeZoneDisplay,
            WindowsTzKey = source.WindowsTzKey,
            TimeServer = source.TimeServer
        };

    private static TimeOnly? ParseTimeLocal(string? timeLocal)
    {
        if (string.IsNullOrWhiteSpace(timeLocal))
        {
            return null;
        }

        var formats = new[] { "HH\\:mm", "H\\:mm" };
        return TimeOnly.TryParseExact(
            timeLocal.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? FormatTimeLocal(TimeOnly? time) =>
        time?.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsDateTimeModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsDateTimeModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
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

    internal sealed class DateTimeWorkResult
    {
        public WindowsDateTimeExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsDateTimeQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static DateTimeWorkResult FromExecuteNow(WindowsDateTimeExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsDateTimeExecuteNowResult.Success(response) };

        public static DateTimeWorkResult FromQueue(WindowsDateTimeQueueResponse response) =>
            new() { QueueResult = WindowsDateTimeQueueResult.Success(response) };

        public static DateTimeWorkResult Failure(string errorCode, string message) =>
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

    private static WindowsDateTimeLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsDateTimeQueueResponse BuildQueueResponse(
        WindowsDateTimeTargetRequest target,
        WindowsDateTimeExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new WindowsDateTimeQueueData
            {
                TaskId = taskId,
                Target = new WindowsDateTimeTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsDateTimeExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsDateTimeExecuteNowResponse BuildExecuteNowResponse(
        WindowsDateTimeTargetRequest target,
        WindowsDateTimeExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsDateTimeExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsDateTimeTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsDateTimeExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsDateTimeBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsDateTimeTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsDateTimeBulkData
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
