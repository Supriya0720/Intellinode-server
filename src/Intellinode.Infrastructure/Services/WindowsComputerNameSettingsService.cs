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

public sealed class WindowsComputerNameSettingsService : IWindowsComputerNameSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsComputerNamePayloadBuilder _payloadBuilder;
    private readonly WindowsComputerNameOptions _options;

    public WindowsComputerNameSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsComputerNamePayloadBuilder payloadBuilder,
        IOptions<WindowsComputerNameOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
    }

    public async Task<WindowsComputerNameExecuteNowResult> ExecuteNowAsync(
        WindowsComputerNameExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsComputerNameExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueComputerNameWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsComputerNameModuleConstants.InstantFunctionName,
                "instant",
                "Computer name instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsComputerNameExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsComputerNameExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsComputerNameQueueResult> QueueAsync(
        WindowsComputerNameQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsComputerNameQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueComputerNameWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsComputerNameModuleConstants.QueuedFunctionName,
                "queued",
                "Computer name scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsComputerNameQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsComputerNameQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsComputerNameCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsComputerNameCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsComputerNameCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsComputerNameSettings;
            var hasSettings = settings is not null;

            return WindowsComputerNameCurrentResult.Success(new WindowsComputerNameCurrentResponse
            {
                Success = true,
                Message = "Computer name settings fetched successfully.",
                Data = new WindowsComputerNameCurrentData
                {
                    Target = new WindowsComputerNameTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new WindowsComputerNameCurrentSettingsDto
                    {
                        ApplyMode = settings?.ApplyMode ?? default,
                        HostName = settings?.HostName ?? string.Empty,
                        Domain = settings?.Domain ?? string.Empty,
                        WorkGroup = settings?.WorkGroup ?? string.Empty,
                        OrganizationalUnit = settings?.OrganizationalUnit ?? string.Empty,
                        UserName = settings?.UserName ?? string.Empty,
                        Password = RedactPassword(settings?.Password),
                        IsDomainJoin = settings?.IsDomainJoin ?? false,
                        Prefix = settings?.Prefix ?? string.Empty,
                        Postfix = settings?.Postfix ?? string.Empty,
                        NoOfChar = settings?.NoOfChar ?? 0,
                        IsMacOrSerial = settings?.IsMacOrSerial ?? false,
                        SettingsVersion = settings?.SettingsVersion ?? 0,
                        PendingApply = settings?.PendingApply ?? false,
                        LastAppliedVersion = settings?.LastAppliedVersion,
                        LastAppliedUtc = settings?.LastAppliedUtc,
                        LastApplyStatus = settings?.LastApplyStatus,
                        LastApplyMessage = settings?.LastApplyMessage
                    },
                    Compat = new WindowsComputerNameCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsComputerNameCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsComputerNameHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsComputerNameHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsComputerNameHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsComputerNameHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleNames = new[]
            {
                WindowsComputerNameModuleConstants.HostRenameModuleName,
                WindowsComputerNameModuleConstants.DomainJoinModuleName
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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsComputerName);

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

            var taskItems = tasks.Select(t => new WindowsComputerNameHistoryItem
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

            var logItems = logs.Select(l => new WindowsComputerNameHistoryItem
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

            return WindowsComputerNameHistoryResult.Success(new WindowsComputerNameHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsComputerNameHistoryData
                {
                    Target = new WindowsComputerNameTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new WindowsComputerNamePagination
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
            return WindowsComputerNameHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsComputerNameBulkResult> ExecuteNowBulkAsync(
        WindowsComputerNameExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsComputerNameBulkResult.Failure(
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
            return WindowsComputerNameBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsComputerNameBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsComputerNameExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsComputerNameBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsComputerNameBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsComputerNameSettings)
                .Include(d => d.Inventory)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsComputerNameTargetRequest
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
            return WindowsComputerNameBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static string RedactPassword(string? password) =>
        string.IsNullOrEmpty(password)
            ? string.Empty
            : WindowsComputerNameSensitiveFields.RedactedPasswordValue;

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsComputerNameSettings)
            .Include(d => d.Inventory)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetComputerNameBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        ComputerNameApplyMode applyMode,
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

    private async Task<ComputerNameWorkResult> QueueComputerNameWorkAsync(
        WindowsComputerNameTargetRequest target,
        WindowsComputerNameSettingsRequest settings,
        WindowsComputerNameExecutionRequest execution,
        WindowsComputerNameOptionsRequest options,
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
            if (string.Equals(functionName, WindowsComputerNameModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return ComputerNameWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return ComputerNameWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return ComputerNameWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return ComputerNameWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
        }

        var queueAttempt = await TryQueueForDeviceAsync(
            device,
            target,
            settings,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            cancellationToken);

        if (!queueAttempt.Success)
        {
            return queueAttempt.Reason switch
            {
                "PayloadTooLarge" => ComputerNameWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsComputerNamePayloadBuilder.MaxFunctionParameterLength} characters."),
                "HostNameNotUnique" => ComputerNameWorkResult.Failure(
                    "ValidationFailed",
                    "Unable to generate a unique host name for this device."),
                _ => ComputerNameWorkResult.Failure("ApplyBlocked", queueAttempt.Reason ?? "ApplyBlocked")
            };
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (string.Equals(functionName, WindowsComputerNameModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return ComputerNameWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return ComputerNameWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<WindowsComputerNameBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsComputerNameTargetRequest> uniqueTargets,
        WindowsComputerNameSettingsRequest settingsTemplate,
        WindowsComputerNameExecutionRequest execution,
        WindowsComputerNameOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken,
        List<Device>? preloadedDevices = null)
    {
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var batchTaskId = Guid.NewGuid();

        if (options.DryRun)
        {
            var dryRunMacs = uniqueTargets.Select(t => t.MacAddress.Trim()).ToList();
            var dryRunDevices = await _dbContext.Devices
                .Include(d => d.Inventory)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && dryRunMacs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            var dryRunByMac = dryRunDevices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);

            var dryRunResults = new List<WindowsComputerNameTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsComputerNameTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                dryRunByMac.TryGetValue(mac, out var device);
                dryRunResults.Add(new WindowsComputerNameTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending",
                    ResolvedHostName = PreviewResolvedHostName(device, settingsTemplate)
                });
            }

            return WindowsComputerNameBulkResult.Success(BuildBulkResponse(
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
                .Include(d => d.WindowsComputerNameSettings)
                .Include(d => d.Inventory)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsComputerNameTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsComputerNameTargetResult
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
                results.Add(new WindowsComputerNameTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var queueAttempt = await TryQueueForDeviceAsync(
                device,
                target,
                settingsTemplate,
                adminId,
                WindowsComputerNameModuleConstants.InstantFunctionName,
                "instant",
                "Computer name bulk instant apply queued.",
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsComputerNameTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsComputerNameTargetResult
            {
                MacAddress = mac,
                Status = "Pending",
                ResolvedHostName = queueAttempt.ResolvedHostName
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsComputerNameBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason, string? ResolvedHostName)> TryQueueForDeviceAsync(
        Device device,
        WindowsComputerNameTargetRequest target,
        WindowsComputerNameSettingsRequest settingsTemplate,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            return (false, null, "EnrollmentStateBlocked", null);
        }

        var blockReason = await GetComputerNameBlockReasonAsync(
            device.Id,
            device.EnrollmentState,
            settingsTemplate.ApplyMode,
            cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason, null);
        }

        var settings = CloneSettings(settingsTemplate);
        var resolvedHostName = await ResolveHostNameForDeviceAsync(device, settings, cancellationToken);
        if (resolvedHostName is null && WindowsComputerNameHostNameGenerator.HasAutoGenerateMetadata(
                settings.HostName,
                settings.Prefix,
                settings.Postfix,
                settings.NoOfChar,
                settings.IsMacOrSerial))
        {
            return (false, null, "HostNameNotUnique", null);
        }

        var functionPayload = BuildFunctionPayload(device, settings);
        if (functionPayload.Length > WindowsComputerNamePayloadBuilder.MaxFunctionParameterLength)
        {
            return (false, null, "PayloadTooLarge", resolvedHostName);
        }

        var now = DateTime.UtcNow;
        var computerNameSettings = device.WindowsComputerNameSettings;
        if (computerNameSettings is null)
        {
            computerNameSettings = new DeviceWindowsComputerNameSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsComputerNameSettings.Add(computerNameSettings);
            device.WindowsComputerNameSettings = computerNameSettings;
        }

        MapSettingsToEntity(settings, computerNameSettings, adminId, now);

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? WindowsComputerNameModuleConstants.DefaultSignalSuffix
            : _options.DefaultSignalSuffix.Trim();
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
            SettingsKind.WindowsComputerName,
            computerNameSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        return (true, task, null, resolvedHostName ?? (string.IsNullOrWhiteSpace(settings.HostName) ? null : settings.HostName.Trim()));
    }

    private async Task<string?> ResolveHostNameForDeviceAsync(
        Device device,
        WindowsComputerNameSettingsRequest settings,
        CancellationToken cancellationToken)
    {
        if (!WindowsComputerNameHostNameGenerator.HasAutoGenerateMetadata(
                settings.HostName,
                settings.Prefix,
                settings.Postfix,
                settings.NoOfChar,
                settings.IsMacOrSerial))
        {
            return null;
        }

        var generated = WindowsComputerNameHostNameGenerator.GenerateHostName(
            device,
            settings.Prefix,
            settings.Postfix,
            settings.NoOfChar,
            settings.IsMacOrSerial);
        var unique = await WindowsComputerNameHostNameGenerator.EnsureUniqueHostNameAsync(
            _dbContext,
            device.TenantId,
            device.Id,
            generated,
            cancellationToken);
        if (unique is null)
        {
            return null;
        }

        settings.HostName = unique;
        return unique;
    }

    private static string? PreviewResolvedHostName(Device? device, WindowsComputerNameSettingsRequest settings)
    {
        if (!WindowsComputerNameHostNameGenerator.HasAutoGenerateMetadata(
                settings.HostName,
                settings.Prefix,
                settings.Postfix,
                settings.NoOfChar,
                settings.IsMacOrSerial))
        {
            return string.IsNullOrWhiteSpace(settings.HostName) ? null : settings.HostName.Trim();
        }

        if (device is null)
        {
            return WindowsComputerNameHostNameGenerator.GenerateHostName(
                new Device { MacAddress = "AA:BB:CC:DD:EE:10:XP" },
                settings.Prefix,
                settings.Postfix,
                settings.NoOfChar,
                settings.IsMacOrSerial);
        }

        return WindowsComputerNameHostNameGenerator.GenerateHostName(
            device,
            settings.Prefix,
            settings.Postfix,
            settings.NoOfChar,
            settings.IsMacOrSerial);
    }

    private static WindowsComputerNameSettingsRequest CloneSettings(WindowsComputerNameSettingsRequest source) =>
        new()
        {
            ApplyMode = source.ApplyMode,
            HostName = source.HostName,
            Domain = source.Domain,
            WorkGroup = source.WorkGroup,
            OrganizationalUnit = source.OrganizationalUnit,
            UserName = source.UserName,
            Password = source.Password,
            IsDomainJoin = source.IsDomainJoin,
            Prefix = source.Prefix,
            Postfix = source.Postfix,
            NoOfChar = source.NoOfChar,
            IsMacOrSerial = source.IsMacOrSerial
        };

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private static WindowsComputerNameBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsComputerNameTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsComputerNameBulkData
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

    private string BuildFunctionPayload(Device device, WindowsComputerNameSettingsRequest settings)
    {
        var macAddr = WindowsComputerNamePayloadBuilder.MapEntityToMacAddr(device.MacAddress);

        if (settings.ApplyMode == ComputerNameApplyMode.HostRename)
        {
            return _payloadBuilder.BuildHostRenamePayload(new WindowsComputerNameHostRenamePayloadRequest
            {
                MacAddr = macAddr,
                HostName = settings.HostName.Trim(),
                Domain = string.Empty,
                WorkGroup = string.Empty,
                UserName = string.Empty,
                Password = string.Empty,
                Prefix = settings.Prefix.Trim(),
                Postfix = settings.Postfix.Trim(),
                NoOfChar = settings.NoOfChar,
                IsMacOrSrNo = settings.IsMacOrSerial,
                TaskID = 0,
                AgentAction = 0
            });
        }

        return _payloadBuilder.BuildDomainJoinPayload(new WindowsComputerNameDomainJoinPayloadRequest
        {
            MacAddr = macAddr,
            IsDomainJoin = settings.IsDomainJoin,
            HostName = settings.HostName.Trim(),
            Domain = settings.IsDomainJoin ? settings.Domain.Trim() : string.Empty,
            WorkGroup = settings.IsDomainJoin ? string.Empty : settings.WorkGroup.Trim(),
            UserName = settings.UserName.Trim(),
            Password = settings.Password,
            OrganizationalUnit = settings.IsDomainJoin ? settings.OrganizationalUnit.Trim() : string.Empty,
            TaskID = 0,
            AgentAction = 0
        });
    }

    private static void MapSettingsToEntity(
        WindowsComputerNameSettingsRequest settings,
        DeviceWindowsComputerNameSettings entity,
        Guid? adminId,
        DateTime now)
    {
        entity.ApplyMode = settings.ApplyMode;
        entity.HostName = settings.HostName.Trim();
        entity.Domain = settings.Domain.Trim();
        entity.WorkGroup = settings.WorkGroup.Trim();
        entity.OrganizationalUnit = settings.OrganizationalUnit.Trim();
        entity.UserName = settings.UserName.Trim();
        entity.Password = settings.Password;
        entity.IsDomainJoin = settings.IsDomainJoin;
        entity.Prefix = settings.Prefix.Trim();
        entity.Postfix = settings.Postfix.Trim();
        entity.NoOfChar = settings.NoOfChar;
        entity.IsMacOrSerial = settings.IsMacOrSerial;
        entity.SettingsVersion++;
        entity.PendingApply = true;
        entity.UpdatedBy = adminId;
        entity.UpdatedUtc = now;
    }

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsComputerNameModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, WindowsComputerNameModuleConstants.QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
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

    private sealed class ComputerNameWorkResult
    {
        public WindowsComputerNameExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsComputerNameQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static ComputerNameWorkResult FromExecuteNow(WindowsComputerNameExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsComputerNameExecuteNowResult.Success(response) };

        public static ComputerNameWorkResult FromQueue(WindowsComputerNameQueueResponse response) =>
            new() { QueueResult = WindowsComputerNameQueueResult.Success(response) };

        public static ComputerNameWorkResult Failure(string errorCode, string message) =>
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

    private static WindowsComputerNameLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsComputerNameQueueResponse BuildQueueResponse(
        WindowsComputerNameTargetRequest target,
        WindowsComputerNameExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new WindowsComputerNameQueueData
            {
                TaskId = taskId,
                Target = new WindowsComputerNameTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsComputerNameExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsComputerNameExecuteNowResponse BuildExecuteNowResponse(
        WindowsComputerNameTargetRequest target,
        WindowsComputerNameExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsComputerNameExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsComputerNameTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsComputerNameExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };
}
