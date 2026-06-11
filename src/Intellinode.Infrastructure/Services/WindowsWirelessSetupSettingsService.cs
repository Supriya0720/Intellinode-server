using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsWirelessSetupSettingsService : IWindowsWirelessSetupSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsWirelessSetupPayloadBuilder _payloadBuilder;
    private readonly WindowsWirelessSetupOptions _options;

    public WindowsWirelessSetupSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsWirelessSetupPayloadBuilder payloadBuilder,
        IOptions<WindowsWirelessSetupOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
    }

    public async Task<WindowsWirelessSetupExecuteNowResult> ExecuteNowAsync(
        WindowsWirelessSetupExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessSetupExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueWirelessWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsWirelessSetupModuleConstants.InstantFunctionName,
                "instant",
                "Wireless instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsWirelessSetupExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsWirelessSetupExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessSetupQueueResult> QueueAsync(
        WindowsWirelessSetupQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessSetupQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueWirelessWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsWirelessSetupModuleConstants.QueuedFunctionName,
                "queued",
                "Wireless scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsWirelessSetupQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsWirelessSetupQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessSetupCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsWirelessSetupCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsWirelessSetupCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsWirelessSetupSettings;
            var hasDesiredRow = settings is not null;
            var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
                ? WindowsWirelessSetupModuleConstants.DefaultSignalSuffix
                : _options.DefaultSignalSuffix.Trim();

            return WindowsWirelessSetupCurrentResult.Success(new WindowsWirelessSetupCurrentResponse
            {
                Success = true,
                Message = "Wireless settings fetched successfully.",
                Data = new WindowsWirelessSetupCurrentData
                {
                    Target = new WindowsWirelessSetupTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Reported = BuildReported(),
                    Desired = BuildDesired(settings),
                    Compat = new WindowsWirelessSetupCompatDto
                    {
                        Source = ResolveCompatSource(hasDesiredRow),
                        ModuleName = WindowsWirelessSetupModuleConstants.ModuleName,
                        SignalSuffix = signalSuffix
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsWirelessSetupCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessSetupHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsWirelessSetupHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsWirelessSetupHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsWirelessSetupHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsWirelessSetupModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsWirelessSetup);

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

            var taskItems = tasks.Select(t => new WindowsWirelessSetupHistoryItem
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

            var logItems = logs.Select(l => new WindowsWirelessSetupHistoryItem
            {
                TaskId = l.TaskId,
                LegacyTaskId = l.LegacyTaskId,
                ModuleName = moduleName,
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

            return WindowsWirelessSetupHistoryResult.Success(new WindowsWirelessSetupHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsWirelessSetupHistoryData
                {
                    Target = new WindowsWirelessSetupTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new WindowsWirelessSetupHistoryPagination
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
            return WindowsWirelessSetupHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessSetupBulkResult> ExecuteNowBulkAsync(
        WindowsWirelessSetupExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessSetupBulkResult.Failure(
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
            return WindowsWirelessSetupBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessSetupBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsWirelessSetupExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessSetupBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsWirelessSetupBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsWirelessSetupSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsWirelessSetupTargetRequest
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
            return WindowsWirelessSetupBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsWirelessSetupSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetWirelessBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return "EnrollmentStateBlocked";
        }

        var hasPendingTask = await _dbContext.DeviceTasks
            .AnyAsync(
                t => t.DeviceId == deviceId &&
                     t.ModuleName == WindowsWirelessSetupModuleConstants.ModuleName &&
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

    private async Task<WirelessWorkResult> QueueWirelessWorkAsync(
        WindowsWirelessSetupTargetRequest target,
        WindowsWirelessSetupSettingsRequest settings,
        WindowsWirelessSetupExecutionRequest execution,
        WindowsWirelessSetupOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var normalizedMac = target.MacAddress.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        if (options.DryRun)
        {
            if (string.Equals(functionName, WindowsWirelessSetupModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return WirelessWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return WirelessWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return WirelessWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return WirelessWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
        }

        var agentAction = ParseAgentAction(execution.AgentAction);
        var queueAttempt = await TryQueueWirelessForDeviceAsync(
            device,
            target,
            settings,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            agentAction,
            acceptedIpsInBatch: null,
            cancellationToken);

        if (!queueAttempt.Success)
        {
            return queueAttempt.Reason switch
            {
                "PayloadTooLarge" => WirelessWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsWirelessSetupPayloadBuilder.MaxFunctionParameterLength} characters."),
                _ => WirelessWorkResult.Failure(
                    queueAttempt.ErrorCode ?? "ApplyBlocked",
                    queueAttempt.Reason ?? "ApplyBlocked")
            };
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (string.Equals(functionName, WindowsWirelessSetupModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return WirelessWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return WirelessWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<WindowsWirelessSetupBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsWirelessSetupTargetRequest> uniqueTargets,
        WindowsWirelessSetupSettingsRequest settingsTemplate,
        WindowsWirelessSetupExecutionRequest execution,
        WindowsWirelessSetupOptionsRequest options,
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

            var dryRunResults = new List<WindowsWirelessSetupTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsWirelessSetupTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                dryRunByMac.TryGetValue(mac, out var device);
                if (device is null)
                {
                    dryRunResults.Add(new WindowsWirelessSetupTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                var previewIp = PreviewAppliedIpAddress(settingsTemplate);
                if (previewIp is null && !settingsTemplate.IsDhcp)
                {
                    dryRunResults.Add(new WindowsWirelessSetupTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "NoReportedIpAddress"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsWirelessSetupTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending",
                    AppliedIpAddress = previewIp
                });
            }

            return WindowsWirelessSetupBulkResult.Success(BuildBulkResponse(
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
                .Include(d => d.WindowsWirelessSetupSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsWirelessSetupTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;
        var acceptedIpsInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsWirelessSetupTargetResult
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
                results.Add(new WindowsWirelessSetupTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var queueAttempt = await TryQueueWirelessForDeviceAsync(
                device,
                target,
                settingsTemplate,
                adminId,
                WindowsWirelessSetupModuleConstants.InstantFunctionName,
                "instant",
                "Wireless bulk instant apply queued.",
                agentAction,
                acceptedIpsInBatch,
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsWirelessSetupTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsWirelessSetupTargetResult
            {
                MacAddress = mac,
                Status = "Pending",
                AppliedIpAddress = queueAttempt.AppliedIpAddress
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsWirelessSetupBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason, string? AppliedIpAddress, string? ErrorCode)> TryQueueWirelessForDeviceAsync(
        Device device,
        WindowsWirelessSetupTargetRequest target,
        WindowsWirelessSetupSettingsRequest settingsTemplate,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        int agentAction,
        HashSet<string>? acceptedIpsInBatch,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            return (false, null, "EnrollmentStateBlocked", null, "ApplyBlocked");
        }

        var blockReason = await GetWirelessBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason, null, "ApplyBlocked");
        }

        var settings = CloneSettings(settingsTemplate);
        if (!settings.IsDhcp)
        {
            if (string.IsNullOrWhiteSpace(settings.IpAddress))
            {
                return (false, null, "NoReportedIpAddress", null, "ValidationFailed");
            }

            if (acceptedIpsInBatch is not null &&
                !acceptedIpsInBatch.Add(settings.IpAddress))
            {
                return (false, null, "DuplicateIpInRequest", null, "ValidationFailed");
            }

            if (_options.ValidateDuplicateIp)
            {
                var duplicateExists = await (
                    from s in _dbContext.DeviceWindowsWirelessSetupSettings
                    join d in _dbContext.Devices on s.DeviceId equals d.Id
                    where d.TenantId == TenantDefaults.DefaultTenantId &&
                          s.DeviceId != device.Id &&
                          s.IpAddress == settings.IpAddress
                    select s).AnyAsync(cancellationToken);
                if (duplicateExists)
                {
                    return (false, null, $"Another device already uses IP address '{settings.IpAddress}'.", null, "ValidationFailed");
                }
            }
        }

        var appliedIpAddress = settings.IsDhcp ? null : settings.IpAddress.Trim();
        var now = DateTime.UtcNow;
        var wirelessSetupSettings = device.WindowsWirelessSetupSettings;
        if (wirelessSetupSettings is null)
        {
            wirelessSetupSettings = new DeviceWindowsWirelessSetupSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsWirelessSetupSettings.Add(wirelessSetupSettings);
            device.WindowsWirelessSetupSettings = wirelessSetupSettings;
        }

        MapSettingsToEntity(settings, wirelessSetupSettings, adminId, now);

        var functionPayload = BuildFunctionPayload(device, settings, agentAction);
        if (functionPayload.Length > WindowsWirelessSetupPayloadBuilder.MaxFunctionParameterLength)
        {
            return (false, null, "PayloadTooLarge", appliedIpAddress, "ValidationFailed");
        }

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? WindowsWirelessSetupModuleConstants.DefaultSignalSuffix
            : _options.DefaultSignalSuffix.Trim();
        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = WindowsWirelessSetupModuleConstants.ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = $"{device.MacAddress.Trim()}&{signalSuffix}",
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.WindowsWirelessSetup,
            wirelessSetupSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        return (true, task, null, appliedIpAddress, null);
    }

    private static string? PreviewAppliedIpAddress(WindowsWirelessSetupSettingsRequest template)
    {
        if (template.IsDhcp)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(template.IpAddress) ? null : template.IpAddress.Trim();
    }

    private static WindowsWirelessSetupSettingsRequest CloneSettings(WindowsWirelessSetupSettingsRequest source) =>
        new()
        {
            IsDhcp = source.IsDhcp,
            IpAddress = source.IpAddress,
            SubnetMask = source.SubnetMask,
            Gateway = source.Gateway,
            PrimaryDns = source.PrimaryDns,
            SecondaryDns = source.SecondaryDns,
            PrimaryWins = source.PrimaryWins,
            SecondaryWins = source.SecondaryWins
        };

    private string BuildFunctionPayload(
        Device device,
        WindowsWirelessSetupSettingsRequest settings,
        int agentAction)
    {
        var macAddr = WindowsWirelessSetupPayloadBuilder.MapEntityToMacAddr(device.MacAddress);

        return _payloadBuilder.BuildWirelessPayload(new WindowsWirelessSetupPayloadRequest
        {
            MacAddr = macAddr,
            Dhcp = settings.IsDhcp,
            IpAddr = settings.IsDhcp ? string.Empty : settings.IpAddress.Trim(),
            SubnetMask = settings.IsDhcp ? string.Empty : settings.SubnetMask.Trim(),
            Gateway = settings.IsDhcp ? string.Empty : settings.Gateway.Trim(),
            PriDns = settings.IsDhcp ? string.Empty : settings.PrimaryDns.Trim(),
            SecDns = settings.IsDhcp ? string.Empty : settings.SecondaryDns.Trim(),
            PriWns = settings.IsDhcp ? string.Empty : settings.PrimaryWins.Trim(),
            SecWns = settings.IsDhcp ? string.Empty : settings.SecondaryWins.Trim(),
            TaskID = 0,
            AgentAction = agentAction
        });
    }

    private static void MapSettingsToEntity(
        WindowsWirelessSetupSettingsRequest settings,
        DeviceWindowsWirelessSetupSettings entity,
        Guid? adminId,
        DateTime now)
    {
        entity.IsDhcp = settings.IsDhcp;

        if (settings.IsDhcp)
        {
            entity.IpAddress = string.Empty;
            entity.SubnetMask = string.Empty;
            entity.Gateway = string.Empty;
            entity.PrimaryDns = string.Empty;
            entity.SecondaryDns = string.Empty;
            entity.PrimaryWins = string.Empty;
            entity.SecondaryWins = string.Empty;
        }
        else
        {
            entity.IpAddress = settings.IpAddress.Trim();
            entity.SubnetMask = settings.SubnetMask.Trim();
            entity.Gateway = settings.Gateway.Trim();
            entity.PrimaryDns = settings.PrimaryDns.Trim();
            entity.SecondaryDns = settings.SecondaryDns.Trim();
            entity.PrimaryWins = settings.PrimaryWins.Trim();
            entity.SecondaryWins = settings.SecondaryWins.Trim();
        }

        entity.SettingsVersion++;
        entity.PendingApply = true;
        entity.UpdatedBy = adminId;
        entity.UpdatedUtc = now;
    }

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private static WindowsWirelessSetupReportedDto BuildReported() =>
        new()
        {
            IsAvailable = false,
            IsDhcp = false,
            IpAddress = string.Empty,
            SubnetMask = string.Empty,
            Gateway = string.Empty,
            PrimaryDns = string.Empty,
            SecondaryDns = string.Empty,
            PrimaryWins = string.Empty,
            SecondaryWins = string.Empty
        };

    private static WindowsWirelessSetupDesiredDto BuildDesired(DeviceWindowsWirelessSetupSettings? settings) =>
        new()
        {
            IsDhcp = settings?.IsDhcp ?? false,
            IpAddress = settings?.IpAddress ?? string.Empty,
            SubnetMask = settings?.SubnetMask ?? string.Empty,
            Gateway = settings?.Gateway ?? string.Empty,
            PrimaryDns = settings?.PrimaryDns ?? string.Empty,
            SecondaryDns = settings?.SecondaryDns ?? string.Empty,
            PrimaryWins = settings?.PrimaryWins ?? string.Empty,
            SecondaryWins = settings?.SecondaryWins ?? string.Empty,
            SettingsVersion = settings?.SettingsVersion ?? 0,
            PendingApply = settings?.PendingApply ?? false,
            LastAppliedVersion = settings?.LastAppliedVersion,
            LastAppliedUtc = settings?.LastAppliedUtc,
            LastApplyStatus = settings?.LastApplyStatus,
            LastApplyMessage = settings?.LastApplyMessage
        };

    private static string ResolveCompatSource(bool hasDesiredRow) =>
        hasDesiredRow ? "device+desired" : "none";

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

    private static string NormalizeScheduleType(string? scheduleType)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
        {
            return "InstantApply";
        }

        return scheduleType.Trim();
    }

    private sealed class WirelessWorkResult
    {
        public WindowsWirelessSetupExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsWirelessSetupQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static WirelessWorkResult FromExecuteNow(WindowsWirelessSetupExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsWirelessSetupExecuteNowResult.Success(response) };

        public static WirelessWorkResult FromQueue(WindowsWirelessSetupQueueResponse response) =>
            new() { QueueResult = WindowsWirelessSetupQueueResult.Success(response) };

        public static WirelessWorkResult Failure(string errorCode, string message) =>
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

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsWirelessSetupModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsWirelessSetupModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "queued";
        }

        return "queued";
    }

    private static WindowsWirelessSetupLegacySummary BuildLegacySummary(string errorMsg) =>
        new()
        {
            ErrorMsg = errorMsg,
            QualifiedMsg = "1",
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsWirelessSetupQueueResponse BuildQueueResponse(
        WindowsWirelessSetupTargetRequest target,
        WindowsWirelessSetupExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new WindowsWirelessSetupQueueData
            {
                TaskId = taskId,
                Target = new WindowsWirelessSetupTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsWirelessSetupExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("...$ApplyGreenSuccess") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsWirelessSetupLegacySummary BuildExecuteNowLegacySummary() =>
        BuildLegacySummary("Wireless setup queued successfully.$ApplyGreenSuccess");

    private static WindowsWirelessSetupExecuteNowResponse BuildExecuteNowResponse(
        WindowsWirelessSetupTargetRequest target,
        WindowsWirelessSetupExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsWirelessSetupExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsWirelessSetupTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsWirelessSetupExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildExecuteNowLegacySummary() : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsWirelessSetupBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsWirelessSetupTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsWirelessSetupBulkData
            {
                TaskId = taskId,
                TotalTargets = totalTargets,
                Accepted = accepted,
                Blocked = blocked,
                Results = results,
                LegacySummary = includeLegacySummary ? BuildLegacySummary("...$ApplyGreenSuccess") : null,
                CorrelationId = correlationId
            }
        };
}

