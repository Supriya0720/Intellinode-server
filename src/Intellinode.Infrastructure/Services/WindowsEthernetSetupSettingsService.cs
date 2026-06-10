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

public sealed class WindowsEthernetSetupSettingsService : IWindowsEthernetSetupSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsEthernetSetupPayloadBuilder _payloadBuilder;
    private readonly WindowsEthernetSetupOptions _options;

    public WindowsEthernetSetupSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsEthernetSetupPayloadBuilder payloadBuilder,
        IOptions<WindowsEthernetSetupOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
    }

    public async Task<WindowsEthernetSetupExecuteNowResult> ExecuteNowAsync(
        WindowsEthernetSetupExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsEthernetSetupExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueEthernetWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsEthernetSetupModuleConstants.InstantFunctionName,
                "instant",
                "Ethernet instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsEthernetSetupExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsEthernetSetupExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsEthernetSetupQueueResult> QueueAsync(
        WindowsEthernetSetupQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsEthernetSetupQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueEthernetWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsEthernetSetupModuleConstants.QueuedFunctionName,
                "queued",
                "Ethernet scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsEthernetSetupQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsEthernetSetupQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsEthernetSetupCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsEthernetSetupCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsEthernetSetupCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsEthernetSetupSettings;
            var hasDesiredRow = settings is not null;
            var hasNetworkData = HasMeaningfulNetworkData(device);
            var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
                ? WindowsEthernetSetupModuleConstants.DefaultSignalSuffix
                : _options.DefaultSignalSuffix.Trim();

            return WindowsEthernetSetupCurrentResult.Success(new WindowsEthernetSetupCurrentResponse
            {
                Success = true,
                Message = "Ethernet settings fetched successfully.",
                Data = new WindowsEthernetSetupCurrentData
                {
                    Target = new WindowsEthernetSetupTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Reported = BuildReported(device, hasNetworkData),
                    Desired = BuildDesired(settings),
                    Compat = new WindowsEthernetSetupCompatDto
                    {
                        Source = ResolveCompatSource(hasNetworkData, hasDesiredRow),
                        ModuleName = WindowsEthernetSetupModuleConstants.ModuleName,
                        SignalSuffix = signalSuffix
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsEthernetSetupCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsEthernetSetupHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsEthernetSetupHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsEthernetSetupHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsEthernetSetupHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsEthernetSetupModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsEthernetSetup);

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

            var taskItems = tasks.Select(t => new WindowsEthernetSetupHistoryItem
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

            var logItems = logs.Select(l => new WindowsEthernetSetupHistoryItem
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

            return WindowsEthernetSetupHistoryResult.Success(new WindowsEthernetSetupHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsEthernetSetupHistoryData
                {
                    Target = new WindowsEthernetSetupTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new WindowsEthernetSetupHistoryPagination
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
            return WindowsEthernetSetupHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsEthernetSetupBulkResult> ExecuteNowBulkAsync(
        WindowsEthernetSetupExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsEthernetSetupBulkResult.Failure(
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
            return WindowsEthernetSetupBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsEthernetSetupBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsEthernetSetupExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsEthernetSetupBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsEthernetSetupBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsEthernetSetupSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsEthernetSetupTargetRequest
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
            return WindowsEthernetSetupBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsEthernetSetupSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetEthernetBlockReasonAsync(
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
                     t.ModuleName == WindowsEthernetSetupModuleConstants.ModuleName &&
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

    private async Task<EthernetWorkResult> QueueEthernetWorkAsync(
        WindowsEthernetSetupTargetRequest target,
        WindowsEthernetSetupSettingsRequest settings,
        WindowsEthernetSetupExecutionRequest execution,
        WindowsEthernetSetupOptionsRequest options,
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
            if (string.Equals(functionName, WindowsEthernetSetupModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return EthernetWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return EthernetWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return EthernetWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return EthernetWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
        }

        var agentAction = ParseAgentAction(execution.AgentAction);
        var queueAttempt = await TryQueueEthernetForDeviceAsync(
            device,
            target,
            settings,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            useDeviceReportedIpForManual: false,
            agentAction,
            acceptedIpsInBatch: null,
            cancellationToken);

        if (!queueAttempt.Success)
        {
            return queueAttempt.Reason switch
            {
                "PayloadTooLarge" => EthernetWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsEthernetSetupPayloadBuilder.MaxFunctionParameterLength} characters."),
                _ => EthernetWorkResult.Failure(
                    queueAttempt.ErrorCode ?? "ApplyBlocked",
                    queueAttempt.Reason ?? "ApplyBlocked")
            };
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (string.Equals(functionName, WindowsEthernetSetupModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return EthernetWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return EthernetWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<WindowsEthernetSetupBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsEthernetSetupTargetRequest> uniqueTargets,
        WindowsEthernetSetupSettingsRequest settingsTemplate,
        WindowsEthernetSetupExecutionRequest execution,
        WindowsEthernetSetupOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken,
        List<Device>? preloadedDevices = null)
    {
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var batchTaskId = Guid.NewGuid();
        var useDeviceReportedIpForManual = ShouldUseDeviceReportedIpForManual(settingsTemplate, uniqueTargets.Count);
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

            var dryRunResults = new List<WindowsEthernetSetupTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsEthernetSetupTargetResult
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
                    dryRunResults.Add(new WindowsEthernetSetupTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                var previewIp = PreviewAppliedIpAddress(device, settingsTemplate, useDeviceReportedIpForManual);
                if (previewIp is null && !settingsTemplate.IsDhcp)
                {
                    dryRunResults.Add(new WindowsEthernetSetupTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "NoReportedIpAddress"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsEthernetSetupTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending",
                    AppliedIpAddress = previewIp
                });
            }

            return WindowsEthernetSetupBulkResult.Success(BuildBulkResponse(
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
                .Include(d => d.WindowsEthernetSetupSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsEthernetSetupTargetResult>(uniqueTargets.Count);
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
                results.Add(new WindowsEthernetSetupTargetResult
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
                results.Add(new WindowsEthernetSetupTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var queueAttempt = await TryQueueEthernetForDeviceAsync(
                device,
                target,
                settingsTemplate,
                adminId,
                WindowsEthernetSetupModuleConstants.InstantFunctionName,
                "instant",
                "Ethernet bulk instant apply queued.",
                useDeviceReportedIpForManual,
                agentAction,
                acceptedIpsInBatch,
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsEthernetSetupTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsEthernetSetupTargetResult
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

        return WindowsEthernetSetupBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason, string? AppliedIpAddress, string? ErrorCode)> TryQueueEthernetForDeviceAsync(
        Device device,
        WindowsEthernetSetupTargetRequest target,
        WindowsEthernetSetupSettingsRequest settingsTemplate,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        bool useDeviceReportedIpForManual,
        int agentAction,
        HashSet<string>? acceptedIpsInBatch,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            return (false, null, "EnrollmentStateBlocked", null, "ApplyBlocked");
        }

        var blockReason = await GetEthernetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason, null, "ApplyBlocked");
        }

        var settings = ResolveSettingsForDevice(settingsTemplate, device, useDeviceReportedIpForManual);
        if (!settings.IsDhcp)
        {
            if (useDeviceReportedIpForManual && string.IsNullOrWhiteSpace(settings.IpAddress))
            {
                return (false, null, "NoReportedIpAddress", null, "ValidationFailed");
            }

            if (acceptedIpsInBatch is not null &&
                !string.IsNullOrWhiteSpace(settings.IpAddress) &&
                !acceptedIpsInBatch.Add(settings.IpAddress))
            {
                return (false, null, "DuplicateIpInRequest", null, "ValidationFailed");
            }

            if (_options.ValidateDuplicateIp)
            {
                var duplicateExists = await _dbContext.Devices
                    .AnyAsync(
                        d => d.TenantId == TenantDefaults.DefaultTenantId &&
                             d.Id != device.Id &&
                             d.IpAddress == settings.IpAddress,
                        cancellationToken);
                if (duplicateExists)
                {
                    return (false, null, $"Another device already uses IP address '{settings.IpAddress}'.", null, "ValidationFailed");
                }
            }
        }

        var appliedIpAddress = settings.IsDhcp ? null : (string.IsNullOrWhiteSpace(settings.IpAddress) ? null : settings.IpAddress);
        var now = DateTime.UtcNow;
        var ethernetSettings = device.WindowsEthernetSetupSettings;
        if (ethernetSettings is null)
        {
            ethernetSettings = new DeviceWindowsEthernetSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsEthernetSettings.Add(ethernetSettings);
            device.WindowsEthernetSetupSettings = ethernetSettings;
        }

        MapSettingsToEntity(settings, ethernetSettings, adminId, now);

        var functionPayload = BuildFunctionPayload(device, settings, agentAction);
        if (functionPayload.Length > WindowsEthernetSetupPayloadBuilder.MaxFunctionParameterLength)
        {
            return (false, null, "PayloadTooLarge", appliedIpAddress, "ValidationFailed");
        }

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? WindowsEthernetSetupModuleConstants.DefaultSignalSuffix
            : _options.DefaultSignalSuffix.Trim();
        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = WindowsEthernetSetupModuleConstants.ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = $"{device.MacAddress.Trim()}&{signalSuffix}",
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.WindowsEthernetSetup,
            ethernetSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        return (true, task, null, appliedIpAddress, null);
    }

    private static WindowsEthernetSetupSettingsRequest ResolveSettingsForDevice(
        WindowsEthernetSetupSettingsRequest template,
        Device device,
        bool useDeviceReportedIpForManual)
    {
        var settings = CloneSettings(template);
        if (settings.IsDhcp || !useDeviceReportedIpForManual)
        {
            return settings;
        }

        settings.IpAddress = device.IpAddress.Trim();
        return settings;
    }

    private static string? PreviewAppliedIpAddress(
        Device? device,
        WindowsEthernetSetupSettingsRequest template,
        bool useDeviceReportedIpForManual)
    {
        if (template.IsDhcp)
        {
            return null;
        }

        if (useDeviceReportedIpForManual)
        {
            return string.IsNullOrWhiteSpace(device?.IpAddress) ? null : device.IpAddress.Trim();
        }

        return string.IsNullOrWhiteSpace(template.IpAddress) ? null : template.IpAddress.Trim();
    }

    private static bool ShouldUseDeviceReportedIpForManual(
        WindowsEthernetSetupSettingsRequest settings,
        int targetCount) =>
        !settings.IsDhcp &&
        targetCount > 1 &&
        string.IsNullOrWhiteSpace(settings.IpAddress);

    private static WindowsEthernetSetupSettingsRequest CloneSettings(WindowsEthernetSetupSettingsRequest source) =>
        new()
        {
            IsDhcp = source.IsDhcp,
            IpAddress = source.IpAddress,
            SubnetMask = source.SubnetMask,
            Gateway = source.Gateway,
            PrimaryDns = source.PrimaryDns,
            SecondaryDns = source.SecondaryDns,
            PrimaryWins = source.PrimaryWins,
            SecondaryWins = source.SecondaryWins,
            ObtainDnsAutomatically = source.ObtainDnsAutomatically,
            NetworkSpeed = source.NetworkSpeed
        };

    private string BuildFunctionPayload(
        Device device,
        WindowsEthernetSetupSettingsRequest settings,
        int agentAction)
    {
        var macAddr = WindowsEthernetSetupPayloadBuilder.MapEntityToMacAddr(device.MacAddress);
        var obtainDnsAutomatically = settings.ObtainDnsAutomatically;

        return _payloadBuilder.BuildEthernetPayload(new WindowsEthernetSetupPayloadRequest
        {
            MacAddr = macAddr,
            Dhcp = settings.IsDhcp,
            IpAddr = settings.IsDhcp ? string.Empty : settings.IpAddress.Trim(),
            SubnetMask = settings.IsDhcp ? string.Empty : settings.SubnetMask.Trim(),
            Gateway = settings.IsDhcp ? string.Empty : settings.Gateway.Trim(),
            PriDns = settings.IsDhcp || obtainDnsAutomatically ? string.Empty : settings.PrimaryDns.Trim(),
            SecDns = settings.IsDhcp || obtainDnsAutomatically ? string.Empty : settings.SecondaryDns.Trim(),
            PriWns = settings.IsDhcp ? string.Empty : settings.PrimaryWins.Trim(),
            SecWns = settings.IsDhcp ? string.Empty : settings.SecondaryWins.Trim(),
            NetworkSpeed = string.IsNullOrWhiteSpace(settings.NetworkSpeed) ? "AutoSelect" : settings.NetworkSpeed.Trim(),
            IsObtainedDnsAutomatically = obtainDnsAutomatically,
            TaskID = 0,
            AgentAction = agentAction
        });
    }

    private static void MapSettingsToEntity(
        WindowsEthernetSetupSettingsRequest settings,
        DeviceWindowsEthernetSettings entity,
        Guid? adminId,
        DateTime now)
    {
        entity.IsDhcp = settings.IsDhcp;
        entity.ObtainDnsAutomatically = settings.ObtainDnsAutomatically;
        entity.NetworkSpeed = string.IsNullOrWhiteSpace(settings.NetworkSpeed)
            ? "AutoSelect"
            : settings.NetworkSpeed.Trim();

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

    private static WindowsEthernetSetupReportedDto BuildReported(Device device, bool hasNetworkData) =>
        new()
        {
            IsAvailable = hasNetworkData,
            IsDhcp = hasNetworkData && device.IsDhcp,
            IpAddress = hasNetworkData ? device.IpAddress : string.Empty,
            SubnetMask = hasNetworkData ? device.SubnetMask : string.Empty,
            Gateway = hasNetworkData ? device.Gateway : string.Empty,
            PrimaryDns = hasNetworkData ? device.PrimaryDns : string.Empty,
            SecondaryDns = hasNetworkData ? device.SecondaryDns : string.Empty,
            PrimaryWins = hasNetworkData ? device.PrimaryWins : string.Empty,
            SecondaryWins = hasNetworkData ? device.SecondaryWins : string.Empty,
            ObtainDnsAutomatically = false
        };

    private static WindowsEthernetSetupDesiredDto BuildDesired(DeviceWindowsEthernetSettings? settings) =>
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
            ObtainDnsAutomatically = settings?.ObtainDnsAutomatically ?? false,
            NetworkSpeed = settings?.NetworkSpeed ?? "AutoSelect",
            SettingsVersion = settings?.SettingsVersion ?? 0,
            PendingApply = settings?.PendingApply ?? false,
            LastAppliedVersion = settings?.LastAppliedVersion,
            LastAppliedUtc = settings?.LastAppliedUtc,
            LastApplyStatus = settings?.LastApplyStatus,
            LastApplyMessage = settings?.LastApplyMessage
        };

    private static bool HasMeaningfulNetworkData(Device device) =>
        device.IsDhcp ||
        !string.IsNullOrWhiteSpace(device.IpAddress) ||
        !string.IsNullOrWhiteSpace(device.SubnetMask) ||
        !string.IsNullOrWhiteSpace(device.Gateway);

    private static string ResolveCompatSource(bool hasNetworkData, bool hasDesiredRow)
    {
        if (hasDesiredRow)
        {
            return "device+desired";
        }

        return hasNetworkData ? "device" : "none";
    }

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

    private sealed class EthernetWorkResult
    {
        public WindowsEthernetSetupExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsEthernetSetupQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static EthernetWorkResult FromExecuteNow(WindowsEthernetSetupExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsEthernetSetupExecuteNowResult.Success(response) };

        public static EthernetWorkResult FromQueue(WindowsEthernetSetupQueueResponse response) =>
            new() { QueueResult = WindowsEthernetSetupQueueResult.Success(response) };

        public static EthernetWorkResult Failure(string errorCode, string message) =>
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
        if (string.Equals(functionName, WindowsEthernetSetupModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsEthernetSetupModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "queued";
        }

        return "queued";
    }

    private static WindowsEthernetSetupLegacySummary BuildLegacySummary(string errorMsg) =>
        new()
        {
            ErrorMsg = errorMsg,
            QualifiedMsg = "1",
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsEthernetSetupQueueResponse BuildQueueResponse(
        WindowsEthernetSetupTargetRequest target,
        WindowsEthernetSetupExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new WindowsEthernetSetupQueueData
            {
                TaskId = taskId,
                Target = new WindowsEthernetSetupTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsEthernetSetupExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("...$ApplyGreenSuccess") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsEthernetSetupLegacySummary BuildExecuteNowLegacySummary() =>
        BuildLegacySummary("Ethernet setup queued successfully.$ApplyGreenSuccess");

    private static WindowsEthernetSetupExecuteNowResponse BuildExecuteNowResponse(
        WindowsEthernetSetupTargetRequest target,
        WindowsEthernetSetupExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsEthernetSetupExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsEthernetSetupTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsEthernetSetupExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildExecuteNowLegacySummary() : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsEthernetSetupBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsEthernetSetupTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsEthernetSetupBulkData
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
