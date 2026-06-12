using System.Text.Json;
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

public sealed class WindowsPowerManagementSettingsService : IWindowsPowerManagementSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsPowerManagementPayloadBuilder _payloadBuilder;
    private readonly WindowsPowerManagementOptions _options;

    public WindowsPowerManagementSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsPowerManagementPayloadBuilder payloadBuilder,
        IOptions<WindowsPowerManagementOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
    }

    public Task<WindowsPowerManagementExecuteNowResult> ExecuteNowAsync(
        WindowsPowerManagementExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        ExecuteNowInternalAsync(request.Target, request.Settings, request.Execution, request.Options, adminId, cancellationToken);

    public Task<WindowsPowerManagementQueueResult> QueueAsync(
        WindowsPowerManagementQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        QueueInternalAsync(request.Target, request.Settings, request.Execution, request.Options, adminId, cancellationToken);

    public Task<WindowsPowerManagementQueueResult> TemplateQueueAsync(
        WindowsPowerManagementTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        TemplateQueueInternalAsync(request.Target, request.Settings, request.Execution, request.Options, adminId, cancellationToken);

    public async Task<WindowsPowerManagementCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsPowerManagementCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsPowerManagementCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsPowerManagementSettings;
            var hasSettings = settings is not null;

            return WindowsPowerManagementCurrentResult.Success(new WindowsPowerManagementCurrentResponse
            {
                Success = true,
                Message = "Power management settings fetched successfully.",
                Data = new WindowsPowerManagementCurrentData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Settings = hasSettings
                        ? MapCurrentSettingsDto(settings!)
                        : new WindowsPowerManagementCurrentSettingsDto(),
                    Compat = new WindowsPowerManagementCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsPowerManagementHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsPowerManagementHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsPowerManagementHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsPowerManagementHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsPowerManagementModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsPowerManagement);

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

            var taskItems = tasks.Select(t => new WindowsPowerManagementHistoryItem
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

            var logItems = logs.Select(l => new WindowsPowerManagementHistoryItem
            {
                TaskId = l.TaskId,
                LegacyTaskId = l.LegacyTaskId,
                SettingsVersion = l.SettingsVersion,
                ApplyStatus = l.Status.ToString(),
                ApplyMode = l.ApplyMode,
                Message = l.Message,
                CreatedUtc = l.CreatedUtc
            });

            var merged = taskItems.Concat(logItems).OrderByDescending(i => i.CreatedUtc);
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                merged = merged
                    .Where(i => string.Equals(i.ApplyStatus, statusFilter, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(i => i.CreatedUtc);
            }

            var totalCount = merged.Count();
            var page = query.Page <= 0 ? 1 : query.Page;
            var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

            return WindowsPowerManagementHistoryResult.Success(new WindowsPowerManagementHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsPowerManagementHistoryData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = merged.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
                    Pagination = new WindowsPowerManagementPagination
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
            return WindowsPowerManagementHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsPowerManagementBulkResult> ExecuteNowBulkAsync(
        WindowsPowerManagementExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementBulkResult.Failure(
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
            return WindowsPowerManagementBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsPowerManagementBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsPowerManagementExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsPowerManagementBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsPowerManagementSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsPowerManagementTargetRequest
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
            return WindowsPowerManagementBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public Task<WindowsPowerManagementExecuteNowResult> ExecuteNowAdvancedAsync(
        WindowsPowerManagementAdvancedExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        ExecuteNowAdvancedInternalAsync(
            request.Target,
            request.Settings,
            request.Execution,
            request.Options,
            adminId,
            cancellationToken);

    public Task<WindowsPowerManagementQueueResult> QueueAdvancedAsync(
        WindowsPowerManagementAdvancedQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        QueueAdvancedInternalAsync(
            request.Target,
            request.Settings,
            request.Execution,
            request.Options,
            adminId,
            cancellationToken);

    public Task<WindowsPowerManagementQueueResult> TemplateQueueAdvancedAsync(
        WindowsPowerManagementAdvancedTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        TemplateQueueAdvancedInternalAsync(
            request.Target,
            request.Settings,
            request.Execution,
            request.Options,
            adminId,
            cancellationToken);

    public Task<WindowsPowerManagementBulkResult> ExecuteNowAdvancedBulkAsync(
        WindowsPowerManagementAdvancedExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        ExecuteNowAdvancedBulkInternalAsync(request, adminId, cancellationToken);

    public Task<WindowsPowerManagementBulkResult> ExecuteNowAdvancedGroupAsync(
        Guid groupId,
        WindowsPowerManagementAdvancedExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default) =>
        ExecuteNowAdvancedGroupInternalAsync(groupId, request, adminId, cancellationToken);

    private async Task<WindowsPowerManagementExecuteNowResult> ExecuteNowInternalAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var work = await QueuePowerWorkAsync(
                target,
                settings,
                execution,
                options,
                adminId,
                WindowsPowerManagementModuleConstants.InstantFunctionName,
                "instant",
                "Power management instant apply queued.",
                cancellationToken);

            return work.ExecuteNowResult
                ?? WindowsPowerManagementExecuteNowResult.Failure(
                    work.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    work.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<WindowsPowerManagementExecuteNowResult> ExecuteNowAdvancedInternalAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementAdvancedSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var work = await QueueAdvancedPowerWorkAsync(
                target,
                settings,
                execution,
                options,
                adminId,
                WindowsPowerManagementModuleConstants.InstantFunctionName,
                "instant",
                "Power management advanced instant apply queued.",
                cancellationToken);

            return work.ExecuteNowResult
                ?? WindowsPowerManagementExecuteNowResult.Failure(
                    work.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    work.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<WindowsPowerManagementQueueResult> QueueAdvancedInternalAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementAdvancedSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(execution.ScheduleType), "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var work = await QueueAdvancedPowerWorkAsync(
                target,
                settings,
                execution,
                options,
                adminId,
                WindowsPowerManagementModuleConstants.QueuedFunctionName,
                "queued",
                "Power management advanced scheduled queue.",
                cancellationToken);

            return work.QueueResult
                ?? WindowsPowerManagementQueueResult.Failure(
                    work.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    work.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<WindowsPowerManagementBulkResult> ExecuteNowAdvancedBulkInternalAsync(
        WindowsPowerManagementAdvancedExecuteNowBulkRequest request,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now bulk.");
            }

            var uniqueTargets = request.Targets
                .GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return await ExecuteNowAdvancedForTargetsInternalAsync(
                uniqueTargets,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<WindowsPowerManagementBulkResult> ExecuteNowAdvancedGroupInternalAsync(
        Guid groupId,
        WindowsPowerManagementAdvancedExecuteNowGroupRequest request,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsPowerManagementBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsPowerManagementSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsPowerManagementTargetRequest
                {
                    MacAddress = d.MacAddress,
                    OsType = ExtractOsType(d.MacAddress)
                })
                .ToList();

            return await ExecuteNowAdvancedForTargetsInternalAsync(
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
            return WindowsPowerManagementBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<WindowsPowerManagementQueueResult> QueueInternalAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(execution.ScheduleType), "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var work = await QueuePowerWorkAsync(
                target,
                settings,
                execution,
                options,
                adminId,
                WindowsPowerManagementModuleConstants.QueuedFunctionName,
                "queued",
                "Power management scheduled queue.",
                cancellationToken);

            return work.QueueResult
                ?? WindowsPowerManagementQueueResult.Failure(
                    work.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    work.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<WindowsPowerManagementQueueResult> TemplateQueueInternalAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(execution.ScheduleType), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementQueueResult.Failure(
                    "ValidationFailed",
                    "Only QueueTemplate is supported on this endpoint.");
            }

            var work = await QueuePowerWorkAsync(
                target,
                settings,
                execution,
                options,
                adminId,
                WindowsPowerManagementModuleConstants.TemplateQueueFunctionName,
                "template",
                BuildTemplateApplyLogMessage(execution),
                cancellationToken);

            return work.QueueResult
                ?? WindowsPowerManagementQueueResult.Failure(
                    work.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    work.Message ?? "Template queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    private async Task<WindowsPowerManagementQueueResult> TemplateQueueAdvancedInternalAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementAdvancedSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(execution.ScheduleType), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsPowerManagementQueueResult.Failure(
                    "ValidationFailed",
                    "Only QueueTemplate is supported on this endpoint.");
            }

            var work = await QueueAdvancedPowerWorkAsync(
                target,
                settings,
                execution,
                options,
                adminId,
                WindowsPowerManagementModuleConstants.TemplateQueueFunctionName,
                "template",
                BuildTemplateApplyLogMessage(execution),
                cancellationToken);

            return work.QueueResult
                ?? WindowsPowerManagementQueueResult.Failure(
                    work.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    work.Message ?? "Template queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsPowerManagementQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal async Task<PowerWorkResult> QueuePowerWorkAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var normalizedMac = target.MacAddress.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var agentAction = ParseAgentAction(execution.AgentAction);

        if (options.DryRun)
        {
            if (WindowsPowerManagementModuleConstants.IsQueuedApplyFunctionName(functionName))
            {
                return PowerWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    DateTime.UtcNow,
                    ShouldReturnLegacySummary(options),
                    correlationId));
            }

            return PowerWorkResult.FromExecuteNow(BuildExecuteNowResponse(
                target,
                execution,
                Guid.Empty,
                DateTime.UtcNow,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
        if (device is null)
        {
            return PowerWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return PowerWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
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
            return PowerWorkResult.Failure("ApplyBlocked", queueAttempt.Reason ?? "ApplyBlocked");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (WindowsPowerManagementModuleConstants.IsQueuedApplyFunctionName(functionName))
        {
            return PowerWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        return PowerWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    internal async Task<PowerWorkResult> QueueAdvancedPowerWorkAsync(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementAdvancedSettingsRequest settings,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var normalizedMac = target.MacAddress.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var agentAction = ParseAgentAction(execution.AgentAction);

        if (options.DryRun)
        {
            if (WindowsPowerManagementModuleConstants.IsQueuedApplyFunctionName(functionName))
            {
                return PowerWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    DateTime.UtcNow,
                    ShouldReturnLegacySummary(options),
                    correlationId));
            }

            return PowerWorkResult.FromExecuteNow(BuildExecuteNowResponse(
                target,
                execution,
                Guid.Empty,
                DateTime.UtcNow,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
        if (device is null)
        {
            return PowerWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        if (!IsXpDevice(device.MacAddress))
        {
            return PowerWorkResult.Failure("ValidationFailed", "UnsupportedOsType");
        }

        var queueAttempt = await TryQueueAdvancedForDeviceAsync(
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
            return PowerWorkResult.Failure("ApplyBlocked", queueAttempt.Reason ?? "ApplyBlocked");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var task = queueAttempt.Task!;
        if (WindowsPowerManagementModuleConstants.IsQueuedApplyFunctionName(functionName))
        {
            return PowerWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        return PowerWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private async Task<WindowsPowerManagementBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsPowerManagementTargetRequest> uniqueTargets,
        WindowsPowerManagementSettingsRequest settingsTemplate,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
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

            var dryRunResults = new List<WindowsPowerManagementTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsPowerManagementTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsPowerManagementTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsPowerManagementTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsPowerManagementBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                ShouldReturnLegacySummary(options),
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
                .Include(d => d.WindowsPowerManagementSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsPowerManagementTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsPowerManagementTargetResult
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
                results.Add(new WindowsPowerManagementTargetResult
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
                WindowsPowerManagementModuleConstants.InstantFunctionName,
                "instant",
                "Power management instant apply queued.",
                agentAction,
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsPowerManagementTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsPowerManagementTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsPowerManagementBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private async Task<WindowsPowerManagementBulkResult> ExecuteNowAdvancedForTargetsInternalAsync(
        List<WindowsPowerManagementTargetRequest> uniqueTargets,
        WindowsPowerManagementAdvancedSettingsRequest settingsTemplate,
        WindowsPowerManagementExecutionRequest execution,
        WindowsPowerManagementOptionsRequest options,
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

            var dryRunResults = new List<WindowsPowerManagementTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsPowerManagementTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsPowerManagementTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsPowerManagementTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsPowerManagementBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                ShouldReturnLegacySummary(options),
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
                .Include(d => d.WindowsPowerManagementSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsPowerManagementTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsPowerManagementTargetResult
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
                results.Add(new WindowsPowerManagementTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var queueAttempt = await TryQueueAdvancedForDeviceAsync(
                device,
                settingsTemplate,
                adminId,
                WindowsPowerManagementModuleConstants.InstantFunctionName,
                "instant",
                "Power management advanced instant apply queued.",
                agentAction,
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsPowerManagementTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsPowerManagementTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsPowerManagementBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason)> TryQueueForDeviceAsync(
        Device device,
        WindowsPowerManagementSettingsRequest settingsTemplate,
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

        var blockReason = await GetBlockReasonAsync(device.Id, cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason);
        }

        var planName = settingsTemplate.PlanName.Trim();
        var planExists = await _dbContext.WindowsPowerPlanMasters
            .AnyAsync(p => p.IsActive && p.PlanName == planName, cancellationToken);
        if (!planExists)
        {
            return (false, null, "InvalidPowerPlan");
        }

        var settingsJson = _payloadBuilder.BuildSettingsJsonFromBasic(new WindowsPowerManagementBasicSettingsRequest
        {
            PlanName = planName,
            IsActive = settingsTemplate.IsActive,
            OptionGroups = BuildOptionGroups(settingsTemplate)
        });

        return await TryQueueForDeviceCoreAsync(
            device,
            planName,
            settingsJson,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            agentAction,
            cancellationToken);
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason)> TryQueueAdvancedForDeviceAsync(
        Device device,
        WindowsPowerManagementAdvancedSettingsRequest settingsTemplate,
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

        var blockReason = await GetBlockReasonAsync(device.Id, cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason);
        }

        var planName = settingsTemplate.PlanName.Trim();
        var planExists = await _dbContext.WindowsPowerPlanMasters
            .AnyAsync(p => p.IsActive && p.PlanName == planName, cancellationToken);
        if (!planExists)
        {
            return (false, null, "InvalidPowerPlan");
        }

        var existingJson = device.WindowsPowerManagementSettings?.SettingsJson;
        var settingsJson = _payloadBuilder.MergeAdvancedSettingsJson(existingJson, settingsTemplate);

        return await TryQueueForDeviceCoreAsync(
            device,
            planName,
            settingsJson,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            agentAction,
            cancellationToken);
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason)> TryQueueForDeviceCoreAsync(
        Device device,
        string planName,
        string settingsJson,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        int agentAction,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var powerSettings = device.WindowsPowerManagementSettings;
        if (powerSettings is null)
        {
            powerSettings = new DeviceWindowsPowerManagementSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsPowerManagementSettings.Add(powerSettings);
            device.WindowsPowerManagementSettings = powerSettings;
        }

        powerSettings.SettingsVersion++;
        powerSettings.ActivePlanName = planName;
        powerSettings.AgentAction = agentAction;
        powerSettings.SettingsJson = settingsJson;
        powerSettings.PendingApply = true;
        powerSettings.UpdatedBy = adminId;
        powerSettings.UpdatedUtc = now;

        _dbContext.DeviceWindowsPowerManagementSettingsSnapshots.Add(new DeviceWindowsPowerManagementSettingsSnapshot
        {
            DeviceId = device.Id,
            SettingsVersion = powerSettings.SettingsVersion,
            ActivePlanName = planName,
            AgentAction = agentAction,
            SettingsJson = settingsJson,
            CreatedUtc = now
        });

        var functionPayload = _payloadBuilder.BuildCompactTaskReference(
            powerSettings.SettingsVersion,
            planName);

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? WindowsPowerManagementModuleConstants.DefaultSignalSuffix
            : _options.DefaultSignalSuffix.Trim();

        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = WindowsPowerManagementModuleConstants.ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = _payloadBuilder.BuildExtraData(device.MacAddress, planName, signalSuffix),
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.WindowsPowerManagement,
            powerSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        return (true, task, null);
    }

    private async Task<string?> GetBlockReasonAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var hasPendingTask = await _dbContext.DeviceTasks
            .AnyAsync(
                t => t.DeviceId == deviceId &&
                     t.ModuleName == WindowsPowerManagementModuleConstants.ModuleName &&
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

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsPowerManagementSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    internal static List<WindowsPowerManagementOptionGroup> BuildOptionGroups(WindowsPowerManagementSettingsRequest settings)
    {
        if (settings.OptionGroups is { Count: > 0 })
        {
            return settings.OptionGroups;
        }

        var groups = new List<WindowsPowerManagementOptionGroup>();
        AddSettingGroup(groups, "Display", "Turn off display after", settings.DisplayTimeoutText);
        AddSettingGroup(groups, "Hard disk", "Turn off hard disk after", settings.HardDiskTimeoutText);
        AddSettingGroup(groups, "Sleep", "Sleep after", settings.SleepTimeoutText);

        if (!string.IsNullOrWhiteSpace(settings.PowerButtonAction) ||
            !string.IsNullOrWhiteSpace(settings.SleepButtonAction))
        {
            var buttonSettings = new List<WindowsPowerManagementSettingValue>();
            if (!string.IsNullOrWhiteSpace(settings.PowerButtonAction))
            {
                buttonSettings.Add(new WindowsPowerManagementSettingValue
                {
                    SettingName = "Power button action",
                    SettingValue = settings.PowerButtonAction.Trim()
                });
            }

            if (!string.IsNullOrWhiteSpace(settings.SleepButtonAction))
            {
                buttonSettings.Add(new WindowsPowerManagementSettingValue
                {
                    SettingName = "Sleep button action",
                    SettingValue = settings.SleepButtonAction.Trim()
                });
            }

            groups.Add(new WindowsPowerManagementOptionGroup
            {
                OptionName = "Power buttons and lid",
                Settings = buttonSettings
            });
        }

        AddSettingGroup(groups, "System standby", "System standby", settings.SystemStandbyTimeoutText);
        return groups;
    }

    private static void AddSettingGroup(
        List<WindowsPowerManagementOptionGroup> groups,
        string optionName,
        string settingName,
        string? settingValue)
    {
        if (string.IsNullOrWhiteSpace(settingValue))
        {
            return;
        }

        groups.Add(new WindowsPowerManagementOptionGroup
        {
            OptionName = optionName,
            Settings =
            [
                new WindowsPowerManagementSettingValue
                {
                    SettingName = settingName,
                    SettingValue = settingValue.Trim()
                }
            ]
        });
    }

    internal static WindowsPowerManagementCurrentSettingsDto MapCurrentSettingsDto(DeviceWindowsPowerManagementSettings settings)
    {
        var dto = new WindowsPowerManagementCurrentSettingsDto
        {
            PlanName = settings.ActivePlanName,
            AgentAction = settings.AgentAction,
            SettingsVersion = settings.SettingsVersion,
            PendingApply = settings.PendingApply,
            LastAppliedVersion = settings.LastAppliedVersion,
            LastAppliedUtc = settings.LastAppliedUtc,
            LastApplyStatus = settings.LastApplyStatus,
            LastApplyMessage = settings.LastApplyMessage
        };

        try
        {
            using var document = JsonDocument.Parse(settings.SettingsJson);
            var root = document.RootElement;
            if (root.TryGetProperty("blIsActive", out var activeElement) &&
                activeElement.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                dto.IsActive = activeElement.GetBoolean();
            }

            if (root.TryGetProperty("objPowerOptions", out var optionsElement) &&
                optionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var option in optionsElement.EnumerateArray())
                {
                    if (!option.TryGetProperty("strPowerOptionName", out var optionNameElement) ||
                        !option.TryGetProperty("objPowerSettings", out var settingsElement) ||
                        settingsElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var optionName = optionNameElement.GetString() ?? string.Empty;
                    var group = new WindowsPowerManagementOptionGroup { OptionName = optionName };
                    foreach (var setting in settingsElement.EnumerateArray())
                    {
                        var name = setting.TryGetProperty("strSettingName", out var nameElement)
                            ? nameElement.GetString() ?? string.Empty
                            : string.Empty;
                        var value = setting.TryGetProperty("strSettingValue", out var valueElement)
                            ? valueElement.GetString() ?? string.Empty
                            : string.Empty;
                        group.Settings.Add(new WindowsPowerManagementSettingValue
                        {
                            SettingName = name,
                            SettingValue = value
                        });

                        MapFlatField(dto, optionName, name, value);
                    }

                    dto.OptionGroups.Add(group);
                    if (WindowsPowerManagementCatalog.IsAdvancedOptionGroup(group))
                    {
                        dto.AdvancedOptionGroups.Add(CloneOptionGroup(group));
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return dto;
    }

    private static WindowsPowerManagementOptionGroup CloneOptionGroup(WindowsPowerManagementOptionGroup group) =>
        new()
        {
            OptionName = group.OptionName,
            Settings = group.Settings.Select(s => new WindowsPowerManagementSettingValue
            {
                SettingName = s.SettingName,
                SettingValue = s.SettingValue
            }).ToList()
        };

    private static void MapFlatField(
        WindowsPowerManagementCurrentSettingsDto dto,
        string optionName,
        string settingName,
        string value)
    {
        if (optionName == "Display" && settingName == "Turn off display after")
        {
            dto.DisplayTimeoutText = value;
        }
        else if (optionName == "Hard disk" && settingName == "Turn off hard disk after")
        {
            dto.HardDiskTimeoutText = value;
        }
        else if (optionName == "Sleep" && settingName == "Sleep after")
        {
            dto.SleepTimeoutText = value;
        }
        else if (optionName == "Power buttons and lid" && settingName == "Power button action")
        {
            dto.PowerButtonAction = value;
        }
        else if (optionName == "Power buttons and lid" && settingName == "Sleep button action")
        {
            dto.SleepButtonAction = value;
        }
        else if (optionName == "System standby" && settingName == "System standby")
        {
            dto.SystemStandbyTimeoutText = value;
        }
    }

    private bool ShouldReturnLegacySummary(WindowsPowerManagementOptionsRequest options) =>
        options.ReturnLegacySummary && _options.LegacySummaryEnabled;

    private static WindowsPowerManagementTargetResponse BuildTargetResponse(string macAddress) =>
        new()
        {
            MacAddress = macAddress,
            OsType = ExtractOsType(macAddress)
        };

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private static int ParseAgentAction(string? agentAction)
    {
        if (string.IsNullOrWhiteSpace(agentAction))
        {
            return 0;
        }

        return int.TryParse(agentAction.Trim(), out var parsed) ? parsed : 0;
    }

    private static string NormalizeScheduleType(string scheduleType)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
        {
            return "InstantApply";
        }

        return scheduleType.Trim();
    }

    private static string MapTaskApplyMode(string functionName) =>
        WindowsPowerManagementModuleConstants.MapApplyMode(functionName);

    private static string BuildTemplateApplyLogMessage(WindowsPowerManagementExecutionRequest execution)
    {
        var templateName = execution.TemplateName?.Trim();
        if (execution.TemplateId is > 0 && !string.IsNullOrWhiteSpace(templateName))
        {
            return $"Power management SysView template queue ({templateName}, id {execution.TemplateId.Value}).";
        }

        if (execution.TemplateId is > 0)
        {
            return $"Power management SysView template queue (id {execution.TemplateId.Value}).";
        }

        return "Power management SysView template queue.";
    }

    private static string MapTaskToApplyStatus(DeviceTaskStatus status) => status switch
    {
        DeviceTaskStatus.Pending => "Pending",
        DeviceTaskStatus.InProcess => "Delivered",
        DeviceTaskStatus.Completed => "Applied",
        DeviceTaskStatus.Failed => "Failed",
        _ => "Pending"
    };

    private static string ExtractOsType(string macAddress) => ExtractOsSuffix(macAddress) ?? "XP";

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

    private static WindowsPowerManagementLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsPowerManagementExecuteNowResponse BuildExecuteNowResponse(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsPowerManagementExecuteNowData
            {
                TaskId = taskId,
                Target = BuildTargetResponse(target.MacAddress),
                Execution = new WindowsPowerManagementExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsPowerManagementQueueResponse BuildQueueResponse(
        WindowsPowerManagementTargetRequest target,
        WindowsPowerManagementExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        var scheduleType = NormalizeScheduleType(execution.ScheduleType);
        return new WindowsPowerManagementQueueResponse
        {
            Success = true,
            Message = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase)
                ? "Template queue accepted."
                : "Queue accepted.",
            Data = new WindowsPowerManagementQueueData
            {
                TaskId = taskId,
                Target = BuildTargetResponse(target.MacAddress),
                Execution = new WindowsPowerManagementExecutionResponse
                {
                    ScheduleType = scheduleType,
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                Template = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase) &&
                           execution.TemplateId is > 0
                    ? new WindowsPowerManagementTemplateInfo
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

    private static WindowsPowerManagementBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsPowerManagementTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsPowerManagementBulkData
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

    internal sealed class PowerWorkResult
    {
        public WindowsPowerManagementExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsPowerManagementQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static PowerWorkResult FromExecuteNow(WindowsPowerManagementExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsPowerManagementExecuteNowResult.Success(response) };

        public static PowerWorkResult FromQueue(WindowsPowerManagementQueueResponse response) =>
            new() { QueueResult = WindowsPowerManagementQueueResult.Success(response) };

        public static PowerWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }
}
