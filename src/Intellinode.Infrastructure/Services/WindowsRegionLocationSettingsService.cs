using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsRegionLocationSettingsService : IWindowsRegionLocationSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsRegionLocationPayloadBuilder _payloadBuilder;

    public WindowsRegionLocationSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsRegionLocationPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
    }

    public async Task<WindowsRegionLocationExecuteNowResult> ExecuteNowAsync(
        WindowsRegionLocationExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionLocationExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueRegionLocationWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsRegionLocationModuleConstants.InstantFunctionName,
                "instant",
                "Region and location instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsRegionLocationExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsRegionLocationExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionLocationQueueResult> QueueAsync(
        WindowsRegionLocationQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionLocationQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueRegionLocationWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsRegionLocationModuleConstants.QueuedFunctionName,
                "queued",
                "Region and location scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsRegionLocationQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsRegionLocationQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionLocationCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsRegionLocationCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsRegionLocationCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsRegionLocationSettings;
            var hasSettings = settings is not null;

            return WindowsRegionLocationCurrentResult.Success(new WindowsRegionLocationCurrentResponse
            {
                Success = true,
                Message = "Region and location settings fetched successfully.",
                Data = new WindowsRegionLocationCurrentData
                {
                    Target = new WindowsRegionLocationTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new WindowsRegionLocationCurrentSettingsDto
                    {
                        GeoId = settings?.GeoId ?? default,
                        LocationName = settings?.LocationName ?? string.Empty,
                        LanguageCode = settings?.LanguageCode ?? default,
                        Bcp47Code = settings?.Bcp47Code ?? string.Empty,
                        LanguageDescription = settings?.LanguageDescription ?? string.Empty,
                        AgentAction = settings?.AgentAction ?? 0,
                        SettingsVersion = settings?.SettingsVersion ?? 0,
                        PendingApply = settings?.PendingApply ?? false,
                        LastAppliedVersion = settings?.LastAppliedVersion,
                        LastAppliedUtc = settings?.LastAppliedUtc,
                        LastApplyStatus = settings?.LastApplyStatus,
                        LastApplyMessage = settings?.LastApplyMessage
                    },
                    Compat = new WindowsRegionLocationCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsRegionLocationCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionLocationHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsRegionLocationHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsRegionLocationHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsRegionLocationHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsRegionLocationModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsRegionLocation);

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

            var taskItems = tasks.Select(t => new WindowsRegionLocationHistoryItem
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

            var logItems = logs.Select(l => new WindowsRegionLocationHistoryItem
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

            return WindowsRegionLocationHistoryResult.Success(new WindowsRegionLocationHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsRegionLocationHistoryData
                {
                    Target = new WindowsRegionLocationTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new WindowsRegionLocationPagination
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
            return WindowsRegionLocationHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionLocationBulkResult> ExecuteNowBulkAsync(
        WindowsRegionLocationExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionLocationBulkResult.Failure(
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
            return WindowsRegionLocationBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsRegionLocationBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsRegionLocationExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsRegionLocationBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsRegionLocationBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsRegionLocationSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsRegionLocationTargetRequest
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
            return WindowsRegionLocationBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsRegionLocationSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetRegionLocationBlockReasonAsync(
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

    internal async Task<RegionLocationWorkResult> QueueRegionLocationWorkAsync(
        WindowsRegionLocationTargetRequest target,
        WindowsRegionLocationSettingsRequest settings,
        WindowsRegionLocationExecutionRequest execution,
        WindowsRegionLocationOptionsRequest options,
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
            if (string.Equals(functionName, WindowsRegionLocationModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return RegionLocationWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return RegionLocationWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return RegionLocationWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return RegionLocationWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
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
                "PayloadTooLarge" => RegionLocationWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsRegionLocationPayloadBuilder.MaxFunctionParameterLength} characters."),
                "InvalidGeoLocation" => RegionLocationWorkResult.Failure(
                    "ValidationFailed",
                    "Invalid geographic location."),
                "InvalidRegionLanguage" => RegionLocationWorkResult.Failure(
                    "ValidationFailed",
                    "Invalid region or language."),
                "MasterDataMismatch" => RegionLocationWorkResult.Failure(
                    "ValidationFailed",
                    "Settings do not match reference master data."),
                _ => RegionLocationWorkResult.Failure("ApplyBlocked", queueAttempt.Reason ?? "ApplyBlocked")
            };
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (string.Equals(functionName, WindowsRegionLocationModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return RegionLocationWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return RegionLocationWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<WindowsRegionLocationBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsRegionLocationTargetRequest> uniqueTargets,
        WindowsRegionLocationSettingsRequest settingsTemplate,
        WindowsRegionLocationExecutionRequest execution,
        WindowsRegionLocationOptionsRequest options,
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

            var dryRunResults = new List<WindowsRegionLocationTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsRegionLocationTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsRegionLocationTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsRegionLocationTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsRegionLocationBulkResult.Success(BuildBulkResponse(
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
                .Include(d => d.WindowsRegionLocationSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsRegionLocationTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsRegionLocationTargetResult
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
                results.Add(new WindowsRegionLocationTargetResult
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
                WindowsRegionLocationModuleConstants.InstantFunctionName,
                "instant",
                "Region and location bulk instant apply queued.",
                agentAction,
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsRegionLocationTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsRegionLocationTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsRegionLocationBulkResult.Success(BuildBulkResponse(
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
        WindowsRegionLocationSettingsRequest settingsTemplate,
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

        var blockReason = await GetRegionLocationBlockReasonAsync(
            device.Id,
            device.EnrollmentState,
            cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason);
        }

        var masterDataReason = await ValidateMasterDataAsync(settings, cancellationToken);
        if (masterDataReason is not null)
        {
            return (false, null, masterDataReason);
        }

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var functionPayload = BuildFunctionPayload(settings, legacyTaskId, agentAction);
        if (functionPayload.Length > WindowsRegionLocationPayloadBuilder.MaxFunctionParameterLength)
        {
            return (false, null, "PayloadTooLarge");
        }

        var now = DateTime.UtcNow;
        var regionLocationSettings = device.WindowsRegionLocationSettings;
        if (regionLocationSettings is null)
        {
            regionLocationSettings = new DeviceWindowsRegionLocationSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsRegionLocationSettings.Add(regionLocationSettings);
            device.WindowsRegionLocationSettings = regionLocationSettings;
        }

        MapSettingsToEntity(settings, regionLocationSettings, agentAction, adminId, now);

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
            SettingsKind.WindowsRegionLocation,
            regionLocationSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        return (true, task, null);
    }

    private async Task<string?> ValidateMasterDataAsync(
        WindowsRegionLocationSettingsRequest settings,
        CancellationToken cancellationToken)
    {
        if (settings.GeoId == WindowsRegionLocationModuleConstants.ExcludedWorldGeoId ||
            string.Equals(settings.LocationName.Trim(), "World", StringComparison.OrdinalIgnoreCase))
        {
            return "InvalidGeoLocation";
        }

        var locationMaster = await _dbContext.RegionAndLocationMasters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.Id == settings.GeoId &&
                     m.Identifier == 'L' &&
                     m.IsActive,
                cancellationToken);

        if (locationMaster is null)
        {
            return "InvalidGeoLocation";
        }

        var regionMaster = await _dbContext.RegionAndLocationMasters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.Id == settings.LanguageCode &&
                     m.Identifier == 'R' &&
                     m.IsActive,
                cancellationToken);

        if (regionMaster is null)
        {
            return "InvalidRegionLanguage";
        }

        var bcp47Code = settings.Bcp47Code.Trim();
        var masterBcp47Code = regionMaster.Bcp47Code?.Trim() ?? string.Empty;
        if (!string.Equals(bcp47Code, masterBcp47Code, StringComparison.Ordinal))
        {
            return "InvalidRegionLanguage";
        }

        var locationName = settings.LocationName.Trim();
        var languageDescription = settings.LanguageDescription.Trim();
        if (!string.Equals(locationName, locationMaster.Value, StringComparison.Ordinal) ||
            !string.Equals(languageDescription, regionMaster.Value, StringComparison.Ordinal))
        {
            return "MasterDataMismatch";
        }

        return null;
    }

    private string BuildFunctionPayload(
        WindowsRegionLocationSettingsRequest settings,
        int legacyTaskId,
        int agentAction) =>
        _payloadBuilder.BuildPayload(new WindowsRegionLocationPayloadRequest
        {
            GeoId = settings.GeoId,
            LocationName = settings.LocationName.Trim(),
            LanguageCode = settings.LanguageCode,
            Bcp47Code = settings.Bcp47Code.Trim(),
            LanguageDescription = settings.LanguageDescription.Trim(),
            TaskID = legacyTaskId,
            AgentAction = agentAction
        });

    private static void MapSettingsToEntity(
        WindowsRegionLocationSettingsRequest settings,
        DeviceWindowsRegionLocationSettings entity,
        int agentAction,
        Guid? adminId,
        DateTime now)
    {
        entity.GeoId = settings.GeoId;
        entity.LocationName = settings.LocationName.Trim();
        entity.LanguageCode = settings.LanguageCode;
        entity.Bcp47Code = settings.Bcp47Code.Trim();
        entity.LanguageDescription = settings.LanguageDescription.Trim();
        entity.AgentAction = agentAction;
        entity.SettingsVersion++;
        entity.PendingApply = true;
        entity.UpdatedBy = adminId;
        entity.UpdatedUtc = now;
    }

    private static WindowsRegionLocationSettingsRequest CloneSettings(WindowsRegionLocationSettingsRequest source) =>
        new()
        {
            GeoId = source.GeoId,
            LocationName = source.LocationName,
            LanguageCode = source.LanguageCode,
            Bcp47Code = source.Bcp47Code,
            LanguageDescription = source.LanguageDescription
        };

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsRegionLocationModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsRegionLocationModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
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

    internal sealed class RegionLocationWorkResult
    {
        public WindowsRegionLocationExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsRegionLocationQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static RegionLocationWorkResult FromExecuteNow(WindowsRegionLocationExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsRegionLocationExecuteNowResult.Success(response) };

        public static RegionLocationWorkResult FromQueue(WindowsRegionLocationQueueResponse response) =>
            new() { QueueResult = WindowsRegionLocationQueueResult.Success(response) };

        public static RegionLocationWorkResult Failure(string errorCode, string message) =>
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

    private static WindowsRegionLocationLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsRegionLocationQueueResponse BuildQueueResponse(
        WindowsRegionLocationTargetRequest target,
        WindowsRegionLocationExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new WindowsRegionLocationQueueData
            {
                TaskId = taskId,
                Target = new WindowsRegionLocationTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsRegionLocationExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsRegionLocationExecuteNowResponse BuildExecuteNowResponse(
        WindowsRegionLocationTargetRequest target,
        WindowsRegionLocationExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsRegionLocationExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsRegionLocationTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsRegionLocationExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsRegionLocationBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsRegionLocationTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsRegionLocationBulkData
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
