using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Interfaces;
using Intellinode.Application.Validation;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed partial class WindowsTaskbarSettingsService : IWindowsTaskbarSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsTaskbarPayloadBuilder _payloadBuilder;
    private readonly WindowsTaskbarOptions _options;

    public WindowsTaskbarSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsTaskbarPayloadBuilder payloadBuilder,
        IOptions<WindowsTaskbarOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
    }

    public async Task<WindowsTaskbarExecuteNowResult> ExecuteNowAsync(
        WindowsTaskbarExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsTaskbarExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueTaskbarWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsTaskbarModuleConstants.InstantFunctionName,
                "instant",
                "Taskbar instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsTaskbarExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsTaskbarExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsTaskbarQueueResult> QueueAsync(
        WindowsTaskbarQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsTaskbarQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueTaskbarWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsTaskbarModuleConstants.QueuedFunctionName,
                "queued",
                "Taskbar scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsTaskbarQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsTaskbarQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsTaskbarCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsTaskbarCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsTaskbarCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsTaskbarSettings;
            var hasSettings = settings is not null;

            return WindowsTaskbarCurrentResult.Success(new WindowsTaskbarCurrentResponse
            {
                Success = true,
                Message = "Taskbar settings fetched successfully.",
                Data = new WindowsTaskbarCurrentData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Settings = hasSettings
                        ? MapCurrentSettingsDto(settings!)
                        : WindowsTaskbarCurrentSettingsDto.CreateFusionXDefaults(),
                    Compat = new WindowsTaskbarCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "defaults"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsTaskbarCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsTaskbarHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsTaskbarHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsTaskbarHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsTaskbarHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsTaskbarModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsTaskbar);

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

            var taskItems = tasks.Select(t => new WindowsTaskbarHistoryItem
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

            var logItems = logs.Select(l => new WindowsTaskbarHistoryItem
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

            return WindowsTaskbarHistoryResult.Success(new WindowsTaskbarHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsTaskbarHistoryData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = items,
                    Pagination = new WindowsTaskbarPagination
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
            return WindowsTaskbarHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static WindowsTaskbarCurrentSettingsDto MapCurrentSettingsDto(
        DeviceWindowsTaskbarSettings settings) =>
        new()
        {
            LockTaskbar = settings.LockTaskbar,
            AutoHideTaskbar = settings.AutoHideTaskbar,
            KeepTaskbarOnTop = settings.KeepTaskbarOnTop,
            GroupSimilarButtons = settings.GroupSimilarButtons,
            ShowQuickLaunch = settings.ShowQuickLaunch,
            AgentAction = settings.AgentAction,
            SettingsVersion = settings.SettingsVersion,
            PendingApply = settings.PendingApply,
            LastAppliedVersion = settings.LastAppliedVersion,
            LastAppliedUtc = settings.LastAppliedUtc,
            LastApplyStatus = settings.LastApplyStatus,
            LastApplyMessage = settings.LastApplyMessage
        };

    private async Task<TaskbarWorkResult> QueueTaskbarWorkAsync(
        WindowsTaskbarTargetRequest target,
        WindowsTaskbarSettingsRequest settings,
        WindowsTaskbarExecutionRequest execution,
        WindowsTaskbarOptionsRequest options,
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

        if (!WindowsTaskbarRequestValidation.PayloadWithinLimit(settings, agentAction))
        {
            return TaskbarWorkResult.Failure(
                "ValidationFailed",
                $"Serialized agent payload exceeds {WindowsTaskbarModuleConstants.MaxFunctionParameterLength} characters.");
        }

        if (options.DryRun)
        {
            if (WindowsTaskbarModuleConstants.IsQueuedApplyFunctionName(functionName))
            {
                return TaskbarWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return TaskbarWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return TaskbarWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetTaskbarBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return TaskbarWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var taskbar = device.WindowsTaskbarSettings;
        if (taskbar is null)
        {
            taskbar = new DeviceWindowsTaskbarSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsTaskbarSettings.Add(taskbar);
            device.WindowsTaskbarSettings = taskbar;
        }

        ApplySettingsRequest(taskbar, settings, agentAction);
        taskbar.SettingsVersion++;
        taskbar.PendingApply = true;
        taskbar.UpdatedBy = adminId;
        taskbar.UpdatedUtc = now;

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var functionPayload = _payloadBuilder.BuildAgentPayload(
            _payloadBuilder.MapToPayloadRequest(taskbar, legacyTaskId, agentAction));

        if (functionPayload.Length > WindowsTaskbarModuleConstants.MaxFunctionParameterLength)
        {
            return TaskbarWorkResult.Failure(
                "ValidationFailed",
                $"Agent payload exceeds {WindowsTaskbarModuleConstants.MaxFunctionParameterLength} characters ({functionPayload.Length}).");
        }

        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = WindowsTaskbarModuleConstants.ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
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
            taskbar.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (WindowsTaskbarModuleConstants.IsQueuedApplyFunctionName(functionName))
        {
            return TaskbarWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary && _options.LegacySummaryEnabled,
                correlationId));
        }

        return TaskbarWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary && _options.LegacySummaryEnabled,
            correlationId));
    }

    private static void ApplySettingsRequest(
        DeviceWindowsTaskbarSettings entity,
        WindowsTaskbarSettingsRequest request,
        int agentAction)
    {
        entity.LockTaskbar = request.LockTaskbar;
        entity.AutoHideTaskbar = request.AutoHideTaskbar;
        entity.KeepTaskbarOnTop = request.KeepTaskbarOnTop;
        entity.GroupSimilarButtons = request.GroupSimilarButtons;
        entity.ShowQuickLaunch = request.ShowQuickLaunch;
        entity.AgentAction = agentAction;
    }

    private async Task<string?> GetTaskbarBlockReasonAsync(
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
                     t.ModuleName == WindowsTaskbarModuleConstants.ModuleName &&
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
            .Include(d => d.WindowsTaskbarSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private static WindowsTaskbarTargetResponse BuildTargetResponse(string macAddress) =>
        new()
        {
            MacAddress = macAddress,
            OsType = ExtractOsType(macAddress)
        };

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, WindowsTaskbarModuleConstants.InstantFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        return "queued";
    }

    private static string MapTaskToApplyStatus(DeviceTaskStatus status) => status switch
    {
        DeviceTaskStatus.Pending => "Pending",
        DeviceTaskStatus.InProcess => "Delivered",
        DeviceTaskStatus.Completed => "Applied",
        DeviceTaskStatus.Failed => "Failed",
        _ => "Pending"
    };

    private static string NormalizeScheduleType(string? scheduleType)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
        {
            return "InstantApply";
        }

        return scheduleType.Trim();
    }

    private static int ParseAgentAction(string? agentAction)
    {
        if (string.IsNullOrWhiteSpace(agentAction))
        {
            return 0;
        }

        return int.TryParse(agentAction.Trim(), out var value) ? value : 0;
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

    private static WindowsTaskbarLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsTaskbarQueueResponse BuildQueueResponse(
        WindowsTaskbarTargetRequest target,
        WindowsTaskbarExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        var scheduleType = NormalizeScheduleType(execution.ScheduleType);
        return new WindowsTaskbarQueueResponse
        {
            Success = true,
            Message = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase)
                ? "Template queue accepted."
                : "Queue accepted.",
            Data = new WindowsTaskbarQueueData
            {
                TaskId = taskId,
                Target = new WindowsTaskbarTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsTaskbarExecutionResponse
                {
                    ScheduleType = scheduleType,
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                Template = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase) &&
                           execution.TemplateId is > 0
                    ? new WindowsTaskbarTemplateInfo
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

    private static WindowsTaskbarExecuteNowResponse BuildExecuteNowResponse(
        WindowsTaskbarTargetRequest target,
        WindowsTaskbarExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsTaskbarExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsTaskbarTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsTaskbarExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private sealed class TaskbarWorkResult
    {
        public WindowsTaskbarExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsTaskbarQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static TaskbarWorkResult FromExecuteNow(WindowsTaskbarExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsTaskbarExecuteNowResult.Success(response) };

        public static TaskbarWorkResult FromQueue(WindowsTaskbarQueueResponse response) =>
            new() { QueueResult = WindowsTaskbarQueueResult.Success(response) };

        public static TaskbarWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }
}
