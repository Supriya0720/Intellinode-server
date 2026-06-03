using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class SystemSettingService : ISystemSettingService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;

    public SystemSettingService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver)
    {
        _dbContext = dbContext;
        _resolver = resolver;
    }

    public async Task<SystemSettingExecuteNowResult> ExecuteNowAsync(
        SystemSettingExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = request.Target.MacAddress.Trim();
            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return SystemSettingExecuteNowResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var correlationId = request.Options.CorrelationId ?? Guid.NewGuid();
            var now = DateTime.UtcNow;
            if (request.Options.DryRun)
            {
                return SystemSettingExecuteNowResult.Success(BuildSingleResponse(
                    request.Target,
                    request.Execution,
                    Guid.Empty,
                    now,
                    request.Options.ReturnLegacySummary,
                    correlationId,
                    legacyQualifiedMsg: "1"));
            }

            var blockReason = await GetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
            if (blockReason is not null)
            {
                return SystemSettingExecuteNowResult.Failure("ApplyBlocked", blockReason);
            }

            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType, "InstantApply");
            var task = await QueueForDeviceAsync(
                device,
                request.Target,
                request.Settings,
                request.Execution,
                scheduleType,
                adminId,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return SystemSettingExecuteNowResult.Success(BuildSingleResponse(
                request.Target,
                request.Execution,
                task.Id,
                task.CreatedUtc,
                request.Options.ReturnLegacySummary,
                correlationId,
                legacyQualifiedMsg: "1"));
        }
        catch (Exception ex)
        {
            return SystemSettingExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<SystemSettingBulkResult> ExecuteNowBulkAsync(
        SystemSettingExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var correlationId = request.Options.CorrelationId ?? Guid.NewGuid();
            var uniqueTargets = request.Targets
                .GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            var batchTaskId = Guid.NewGuid();

            if (request.Options.DryRun)
            {
                var dryRunResults = uniqueTargets
                    .Select(t => new SystemSettingTargetResult { MacAddress = t.MacAddress.Trim(), Status = "Pending" })
                    .ToList();

                return SystemSettingBulkResult.Success(new SystemSettingBulkResponse
                {
                    Success = true,
                    Message = "Bulk execute-now accepted.",
                    Data = new SystemSettingBulkData
                    {
                        TaskId = batchTaskId,
                        TotalTargets = uniqueTargets.Count,
                        Accepted = uniqueTargets.Count,
                        Blocked = 0,
                        Results = dryRunResults,
                        LegacySummary = request.Options.ReturnLegacySummary
                            ? BuildLegacySummary(uniqueTargets.Count.ToString())
                            : null,
                        CorrelationId = correlationId
                    }
                });
            }

            var devices = await _dbContext.Devices
                .Include(d => d.RemoteSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId)
                .Where(d => uniqueTargets.Select(t => t.MacAddress.Trim()).Contains(d.MacAddress))
                .ToListAsync(cancellationToken);

            var byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
            var results = new List<SystemSettingTargetResult>(uniqueTargets.Count);
            var firstAcceptedTaskId = Guid.Empty;
            var accepted = 0;
            var blocked = 0;
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType, "InstantApply");

            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!byMac.TryGetValue(mac, out var device))
                {
                    blocked++;
                    results.Add(new SystemSettingTargetResult { MacAddress = mac, Status = "Blocked", Reason = "DeviceNotFound" });
                    continue;
                }

                var blockReason = await GetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
                if (blockReason is not null)
                {
                    blocked++;
                    results.Add(new SystemSettingTargetResult { MacAddress = mac, Status = "Blocked", Reason = blockReason });
                    continue;
                }

                var task = await QueueForDeviceAsync(
                    device,
                    target,
                    request.Settings,
                    request.Execution,
                    scheduleType,
                    adminId,
                    cancellationToken);

                firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? task.Id : firstAcceptedTaskId;
                accepted++;
                results.Add(new SystemSettingTargetResult { MacAddress = mac, Status = "Pending" });
            }

            if (accepted > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return SystemSettingBulkResult.Success(new SystemSettingBulkResponse
            {
                Success = true,
                Message = "Bulk execute-now accepted.",
                Data = new SystemSettingBulkData
                {
                    TaskId = firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
                    TotalTargets = uniqueTargets.Count,
                    Accepted = accepted,
                    Blocked = blocked,
                    Results = results,
                    LegacySummary = request.Options.ReturnLegacySummary
                        ? BuildLegacySummary(accepted.ToString())
                        : null,
                    CorrelationId = correlationId
                }
            });
        }
        catch (Exception ex)
        {
            return SystemSettingBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<SystemSettingQueueResult> QueueAsync(
        SystemSettingQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        return await QueueSingleAsync(
            request.Target,
            request.Settings,
            request.Execution,
            request.Options,
            "Queue",
            adminId,
            cancellationToken);
    }

    public async Task<SystemSettingQueueResult> TemplateQueueAsync(
        SystemSettingTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        return await QueueSingleAsync(
            request.Target,
            request.Settings,
            request.Execution,
            request.Options,
            "QueueTemplate",
            adminId,
            cancellationToken);
    }

    public async Task<SystemSettingCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return SystemSettingCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .Include(d => d.RemoteSettings)
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return SystemSettingCurrentResult.Failure("DeviceNotFound", $"No device found with MAC address '{normalizedMac}'.");
            }

            var effective = await _resolver.ResolveEffectiveCombinedByMacAsync(normalizedMac, cancellationToken);
            if (effective is null)
            {
                return SystemSettingCurrentResult.Failure("DeviceNotFound", $"No device found with MAC address '{normalizedMac}'.");
            }

            var groupSettings = device.GroupId.HasValue
                ? await _dbContext.GroupRemoteSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.GroupId == device.GroupId.Value, cancellationToken)
                : null;
            var tenantDefaults = await _resolver.GetTenantDefaultsAsync(device.TenantId, cancellationToken);

            var source = MapSource(effective.GeneralSource);
            var sourceServerHost = source == "Group" ? groupSettings?.ServerHost : device.RemoteSettings?.ServerHost;
            var sourceServerPort = source == "Group" ? groupSettings?.ServerPort ?? 0 : device.RemoteSettings?.ServerPort ?? 0;
            var sourcePoll = source == "Group" ? groupSettings?.PollIntervalSeconds : device.RemoteSettings?.PollIntervalSeconds;
            var sourceComm = source == "Group" ? groupSettings?.CommunicationType : device.RemoteSettings?.CommunicationType;
            var sourceEnabled = source == "Group" ? groupSettings?.AgentEnabled : device.RemoteSettings?.AgentEnabled;
            var sourceGroupName = source == "Group" ? groupSettings?.DesiredGroupName : device.RemoteSettings?.DesiredGroupName;
            var sourceHostName = source == "Group" ? groupSettings?.AgentHostName : device.RemoteSettings?.AgentHostName;
            var sourceApplyOnReboot = source == "Group" ? false : device.RemoteSettings?.ApplyOnReboot;
            var sourcePendingApply = source == "Group" ? false : device.RemoteSettings?.PendingApply;
            var sourceVersion = source == "Group" ? groupSettings?.SettingsVersion : device.RemoteSettings?.SettingsVersion;
            var sourceLastAppliedVersion = device.RemoteSettings?.LastAppliedVersion;
            var sourceLastAppliedUtc = device.RemoteSettings?.LastAppliedUtc;

            var serverHost = sourceServerHost;
            var serverPort = sourceServerPort;
            if (string.IsNullOrWhiteSpace(serverHost))
            {
                var hostPort = AgentSettingsHelper.ParseHostPort(tenantDefaults.ServerBaseUrl);
                serverHost = hostPort.Host;
                if (serverPort <= 0)
                {
                    serverPort = hostPort.Port;
                }
            }

            var response = new SystemSettingCurrentResponse
            {
                Success = true,
                Message = "Settings fetched successfully.",
                Data = new SystemSettingCurrentData
                {
                    Target = new SystemSettingTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new SystemSettingCurrentSettingsDto
                    {
                        ServerIpOrHost = serverHost ?? string.Empty,
                        PortNo = serverPort,
                        HeartbeatIntervalSeconds = sourcePoll ?? effective.General.PollIntervalSeconds,
                        CommunicationType = (sourceComm ?? effective.General.CommunicationType).ToString(),
                        ClientStatus = sourceEnabled ?? effective.General.AgentEnabled,
                        GroupName = sourceGroupName,
                        HostName = sourceHostName,
                        ApplyOnReboot = sourceApplyOnReboot ?? false,
                        PendingApply = sourcePendingApply ?? effective.General.PendingApply,
                        SettingsVersion = sourceVersion ?? effective.General.SettingsVersion,
                        LastAppliedVersion = sourceLastAppliedVersion,
                        LastAppliedUtc = sourceLastAppliedUtc
                    },
                    Compat = new SystemSettingCurrentCompatDto
                    {
                        Source = source,
                        LegacySummaryAvailable = true
                    }
                }
            };

            return SystemSettingCurrentResult.Success(response);
        }
        catch (Exception ex)
        {
            return SystemSettingCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<SystemSettingHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        SystemSettingHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return SystemSettingHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return SystemSettingHistoryResult.Failure("DeviceNotFound", $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var tasksQuery = _dbContext.DeviceTasks
                .AsNoTracking()
                .Where(t => t.DeviceId == device.Id);

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
                .Where(l => l.DeviceId == device.Id);

            if (query.FromUtc.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.CreatedUtc >= query.FromUtc.Value);
            }

            if (query.ToUtc.HasValue)
            {
                logsQuery = logsQuery.Where(l => l.CreatedUtc <= query.ToUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<SettingsApplyStatus>(statusFilter, out var parsedApplyStatus))
            {
                logsQuery = logsQuery.Where(l => l.Status == parsedApplyStatus);
            }

            var logs = await logsQuery.ToListAsync(cancellationToken);

            var taskItems = tasks
                .Select(t => new SystemSettingHistoryItem
                {
                    TaskId = t.Id,
                    LegacyTaskId = t.LegacyTaskId,
                    ModuleName = t.ModuleName,
                    FunctionName = t.FunctionName,
                    Status = t.Status.ToString(),
                    ApplyStatus = MapTaskToApplyStatus(t.Status),
                    ApplyMode = t.FunctionName == "InstantApply" ? "instant" : t.FunctionName == "QueueTemplate" ? "template" : "queued",
                    CreatedUtc = t.CreatedUtc
                });

            var logItems = logs
                .Select(l => new SystemSettingHistoryItem
                {
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
                merged = merged.Where(i => string.Equals(i.ApplyStatus, statusFilter, StringComparison.OrdinalIgnoreCase))
                               .OrderByDescending(i => i.CreatedUtc);
            }

            var totalCount = merged.Count();
            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);
            var items = merged
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return SystemSettingHistoryResult.Success(new SystemSettingHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new SystemSettingHistoryData
                {
                    Target = new SystemSettingTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new SystemSettingPagination
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
            return SystemSettingHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static string BuildLegacyFunctionPayload(
        SystemSettingTargetRequest target,
        DeviceRemoteSettings settings,
        TenantAgentDefaults tenantDefaults)
    {
        var basePayload = new
        {
            target.MacAddress,
            target.OsType,
            ServerIpOrHost = settings.ServerHost,
            PortNo = settings.ServerPort,
            HeartbeatIntervalSeconds = settings.PollIntervalSeconds,
            CommunicationType = settings.CommunicationType.ToString(),
            ClientStatus = settings.AgentEnabled,
            GroupName = settings.DesiredGroupName,
            HostName = settings.AgentHostName,
            ApiBaseUrl = tenantDefaults.ApiBaseUrl
        };

        object wrapper = target.OsType.Trim().ToUpperInvariant() switch
        {
            "XP" => new { WinCELinux = new { RemoteSettings = basePayload } },
            "LX" => new { WinCELinux = new { LxRemoteSettings = basePayload } },
            "CE" => new { WinCELinux = new { structAndroidData = new { Global_Values = basePayload } } },
            _ => new { WinCELinux = new { RemoteSettings = basePayload } }
        };

        return JsonSerializer.Serialize(wrapper);
    }

    private async Task<int> GetNextLegacyTaskIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var maxLegacyId = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId)
            .MaxAsync(t => (int?)t.LegacyTaskId, cancellationToken);

        return (maxLegacyId ?? 0) + 1;
    }

    private async Task<SystemSettingQueueResult> QueueSingleAsync(
        SystemSettingTargetRequest target,
        SystemSettingRemoteSettingsRequest settings,
        SystemSettingExecutionRequest execution,
        SystemSettingOptionsRequest options,
        string requiredScheduleType,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedMac = target.MacAddress.Trim();
            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return SystemSettingQueueResult.Failure("DeviceNotFound", $"No device found with MAC address '{normalizedMac}'.");
            }

            var correlationId = options.CorrelationId ?? Guid.NewGuid();
            var now = DateTime.UtcNow;
            if (options.DryRun)
            {
                return SystemSettingQueueResult.Success(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            var blockReason = await GetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
            if (blockReason is not null)
            {
                return SystemSettingQueueResult.Failure("ApplyBlocked", blockReason);
            }

            var scheduleType = NormalizeScheduleType(execution.ScheduleType, requiredScheduleType);
            var task = await QueueForDeviceAsync(
                device,
                target,
                settings,
                execution,
                scheduleType,
                adminId,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return SystemSettingQueueResult.Success(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }
        catch (Exception ex)
        {
            return SystemSettingQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<DeviceTask> QueueForDeviceAsync(
        Device device,
        SystemSettingTargetRequest target,
        SystemSettingRemoteSettingsRequest requestSettings,
        SystemSettingExecutionRequest execution,
        string scheduleType,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        var tenantDefaults = await _resolver.GetTenantDefaultsAsync(device.TenantId, cancellationToken);
        var settings = device.RemoteSettings;
        if (settings is null)
        {
            settings = new DeviceRemoteSettings
            {
                DeviceId = device.Id,
                InheritFromGroup = false,
                SettingsVersion = 0,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            _dbContext.DeviceRemoteSettings.Add(settings);
            device.RemoteSettings = settings;
        }

        settings.ServerHost = requestSettings.ServerIpOrHost.Trim();
        settings.ServerPort = requestSettings.PortNo;
        settings.PollIntervalSeconds = requestSettings.HeartbeatIntervalSeconds;
        settings.CommunicationType = requestSettings.CommunicationType;
        settings.AgentEnabled = requestSettings.ClientStatus;
        settings.DesiredGroupName = requestSettings.GroupName;
        settings.AgentHostName = requestSettings.HostName;
        settings.InheritFromGroup = false;
        settings.ApplyOnReboot = scheduleType != "InstantApply";
        settings.SettingsVersion++;
        settings.PendingApply = true;
        settings.UpdatedUtc = DateTime.UtcNow;

        var functionPayload = BuildLegacyFunctionPayload(target, settings, tenantDefaults);
        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken),
            ModuleName = string.IsNullOrWhiteSpace(execution.ModuleType) ? "SetRemoteSettings" : execution.ModuleType.Trim(),
            FunctionName = scheduleType,
            FunctionParameter = functionPayload,
            ExtraData = BuildExtraData(execution),
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.General,
            settings.SettingsVersion,
            scheduleType == "InstantApply" ? "instant" : "queued",
            SettingsApplyStatus.Pending,
            adminId,
            $"SystemSetting compatibility {scheduleType} queued.",
            cancellationToken);

        return task;
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.RemoteSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetBlockReasonAsync(Guid deviceId, EnrollmentState enrollmentState, CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return "EnrollmentStateBlocked";
        }

        var hasPendingOrInProcess = await _dbContext.DeviceTasks
            .AnyAsync(
                t => t.DeviceId == deviceId &&
                     (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess),
                cancellationToken);

        return hasPendingOrInProcess ? "PendingTaskExists" : null;
    }

    private static string BuildExtraData(SystemSettingExecutionRequest execution)
    {
        if (!string.Equals(execution.ScheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(execution.ModuleName) ? string.Empty : execution.ModuleName.Trim();
        }

        return JsonSerializer.Serialize(new
        {
            execution.ModuleName,
            execution.TemplateId,
            execution.TemplateName
        });
    }

    private static string NormalizeScheduleType(string? scheduleType, string fallback)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
        {
            return fallback;
        }

        var normalized = scheduleType.Trim();
        return normalized switch
        {
            "InstantApply" => "InstantApply",
            "Queue" => "Queue",
            "QueueTemplate" => "QueueTemplate",
            _ => fallback
        };
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

    private static string MapSource(string source) => source switch
    {
        "device" => "Device",
        "group" => "Group",
        _ => "TenantDefault"
    };

    private static string MapTaskToApplyStatus(DeviceTaskStatus status) => status switch
    {
        DeviceTaskStatus.Pending => "Pending",
        DeviceTaskStatus.InProcess => "Delivered",
        DeviceTaskStatus.Completed => "Applied",
        DeviceTaskStatus.Failed => "Failed",
        _ => "Pending"
    };

    private static SystemSettingLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static SystemSettingExecuteNowResponse BuildSingleResponse(
        SystemSettingTargetRequest target,
        SystemSettingExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId,
        string legacyQualifiedMsg)
    {
        return new SystemSettingExecuteNowResponse
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new SystemSettingExecuteNowData
            {
                TaskId = taskId,
                Target = new SystemSettingTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new SystemSettingExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType, "InstantApply"),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary(legacyQualifiedMsg) : null,
                CorrelationId = correlationId
            }
        };
    }

    private static SystemSettingQueueResponse BuildQueueResponse(
        SystemSettingTargetRequest target,
        SystemSettingExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        var scheduleType = NormalizeScheduleType(execution.ScheduleType, "Queue");
        return new SystemSettingQueueResponse
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new SystemSettingQueueData
            {
                TaskId = taskId,
                Target = new SystemSettingTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new SystemSettingExecutionResponse
                {
                    ScheduleType = scheduleType,
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                Template = scheduleType == "QueueTemplate" && execution.TemplateId.HasValue
                    ? new SystemSettingTemplateInfo
                    {
                        TemplateId = execution.TemplateId.Value,
                        TemplateName = execution.TemplateName ?? string.Empty
                    }
                    : null,
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };
    }
}
