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

public sealed class WindowsApplicationCommandSettingsService : IWindowsApplicationCommandSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsApplicationCommandPayloadBuilder _payloadBuilder;
    private readonly WindowsApplicationCommandOptions _options;
    private readonly WindowsApplicationCommandValidationPolicy _validationPolicy;

    public WindowsApplicationCommandSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsApplicationCommandPayloadBuilder payloadBuilder,
        IOptions<WindowsApplicationCommandOptions> options,
        IOptions<WindowsApplicationCommandValidationPolicy> validationPolicy)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
        _validationPolicy = validationPolicy.Value;
    }

    public async Task<WindowsApplicationCommandExecuteNowResult> ExecuteNowAsync(
        WindowsApplicationCommandExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsApplicationCommandExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueApplicationCommandWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsApplicationCommandModuleConstants.InstantFunctionName,
                "instant",
                BuildApplyLogMessage(request.Settings.Mode, instant: true),
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsApplicationCommandExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsApplicationCommandExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsApplicationCommandQueueResult> QueueAsync(
        WindowsApplicationCommandQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsApplicationCommandQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueApplicationCommandWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsApplicationCommandModuleConstants.QueuedFunctionName,
                "queued",
                BuildApplyLogMessage(request.Settings.Mode, instant: false),
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsApplicationCommandQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsApplicationCommandQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsApplicationCommandQueueResult> TemplateQueueAsync(
        WindowsApplicationCommandTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsApplicationCommandQueueResult.Failure(
                    "ValidationFailed",
                    "Only QueueTemplate is supported on this endpoint.");
            }

            var queueResult = await QueueApplicationCommandWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsApplicationCommandModuleConstants.TemplateQueueFunctionName,
                "template",
                BuildTemplateApplyLogMessage(request.Settings.Mode, request.Execution),
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsApplicationCommandQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Template queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsApplicationCommandQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsApplicationCommandBulkResult> ExecuteNowBulkAsync(
        WindowsApplicationCommandExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsApplicationCommandBulkResult.Failure(
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
            return WindowsApplicationCommandBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsApplicationCommandBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsApplicationCommandExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsApplicationCommandBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsApplicationCommandBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsApplicationCommandSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsApplicationCommandTargetRequest
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
            return WindowsApplicationCommandBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsApplicationCommandCurrentResult> GetCurrentAsync(
        string macAddress,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsApplicationCommandCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            if (!string.IsNullOrWhiteSpace(mode) && !IsSupportedMode(mode))
            {
                return WindowsApplicationCommandCurrentResult.Failure(
                    "ValidationFailed",
                    "mode must be Application or Command when supplied.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsApplicationCommandCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsApplicationCommandSettings;
            var requestedMode = string.IsNullOrWhiteSpace(mode)
                ? (WindowsApplicationCommandMode?)null
                : WindowsApplicationCommandModuleConstants.ParseMode(mode);

            var hasMatchingSettings = settings is not null
                && (requestedMode is null
                    || string.Equals(
                        settings.Mode,
                        WindowsApplicationCommandModuleConstants.FormatMode(requestedMode.Value),
                        StringComparison.OrdinalIgnoreCase));

            var defaultsMode = requestedMode ?? WindowsApplicationCommandMode.Application;

            return WindowsApplicationCommandCurrentResult.Success(new WindowsApplicationCommandCurrentResponse
            {
                Success = true,
                Message = "Application command settings fetched successfully.",
                Data = new WindowsApplicationCommandCurrentData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Settings = hasMatchingSettings
                        ? MapCurrentSettingsDto(settings!)
                        : WindowsApplicationCommandCurrentSettingsDto.CreateFusionXDefaults(defaultsMode),
                    Compat = new WindowsApplicationCommandCurrentCompatDto
                    {
                        Source = hasMatchingSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsApplicationCommandCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsApplicationCommandHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsApplicationCommandHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsApplicationCommandHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsApplicationCommandHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleNames = new[]
            {
                WindowsApplicationCommandModuleConstants.ApplicationModuleName,
                WindowsApplicationCommandModuleConstants.CommandModuleName
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
                .Where(l => l.DeviceId == device.Id &&
                            (l.SettingsKind == SettingsKind.WindowsApplication ||
                             l.SettingsKind == SettingsKind.WindowsCommand));

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

            var taskItems = tasks.Select(t => new WindowsApplicationCommandHistoryItem
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

            var logItems = logs.Select(l => new WindowsApplicationCommandHistoryItem
            {
                TaskId = l.TaskId,
                LegacyTaskId = l.LegacyTaskId,
                ModuleName = l.SettingsKind == SettingsKind.WindowsCommand
                    ? WindowsApplicationCommandModuleConstants.CommandModuleName
                    : WindowsApplicationCommandModuleConstants.ApplicationModuleName,
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

            return WindowsApplicationCommandHistoryResult.Success(new WindowsApplicationCommandHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsApplicationCommandHistoryData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = items,
                    Pagination = new WindowsApplicationCommandPagination
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
            return WindowsApplicationCommandHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static WindowsApplicationCommandCurrentSettingsDto MapCurrentSettingsDto(
        DeviceWindowsApplicationCommandSettings settings) =>
        new()
        {
            Mode = settings.Mode,
            ApplicationPath = settings.ApplicationPath,
            Parameters = settings.Parameters,
            WarnUser = settings.WarnUser,
            AlertTitle = settings.AlertTitle,
            AlertMessage = settings.AlertMessage,
            MessageType = settings.MessageType,
            DisplayTime = settings.DisplayTime,
            CommandText = settings.CommandText,
            Timeout = settings.Timeout,
            RebootRequired = settings.RebootRequired,
            RequireCommandOutput = settings.RequireCommandOutput,
            AgentAction = settings.AgentAction,
            SettingsVersion = settings.SettingsVersion,
            PendingApply = settings.PendingApply,
            LastAppliedVersion = settings.LastAppliedVersion,
            LastAppliedUtc = settings.LastAppliedUtc,
            LastApplyStatus = settings.LastApplyStatus,
            LastApplyMessage = settings.LastApplyMessage
        };

    private async Task<ApplicationCommandWorkResult> QueueApplicationCommandWorkAsync(
        WindowsApplicationCommandTargetRequest target,
        WindowsApplicationCommandSettingsRequest settings,
        WindowsApplicationCommandExecutionRequest execution,
        WindowsApplicationCommandOptionsRequest options,
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
        var moduleName = WindowsApplicationCommandModuleConstants.ResolveModuleName(settings.Mode);
        var settingsKind = WindowsApplicationCommandModuleConstants.ResolveSettingsKind(settings.Mode);

        var validationError = WindowsApplicationCommandRequestValidation.ValidateSettings(settings, _validationPolicy);
        if (validationError is not null)
        {
            return ApplicationCommandWorkResult.Failure("ValidationFailed", validationError);
        }

        if (!WindowsApplicationCommandRequestValidation.PayloadWithinLimit(settings, agentAction))
        {
            return ApplicationCommandWorkResult.Failure(
                "ValidationFailed",
                $"Serialized agent payload exceeds {WindowsApplicationCommandModuleConstants.MaxFunctionParameterLength} characters.");
        }

        if (options.DryRun)
        {
            if (WindowsApplicationCommandModuleConstants.IsQueuedApplyFunctionName(functionName))
            {
                return ApplicationCommandWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return ApplicationCommandWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return ApplicationCommandWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetApplicationCommandBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return ApplicationCommandWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var appCommandSettings = device.WindowsApplicationCommandSettings;
        if (appCommandSettings is null)
        {
            appCommandSettings = new DeviceWindowsApplicationCommandSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsApplicationCommandSettings.Add(appCommandSettings);
            device.WindowsApplicationCommandSettings = appCommandSettings;
        }

        ApplySettingsRequest(appCommandSettings, settings, agentAction);
        appCommandSettings.SettingsVersion++;
        appCommandSettings.PendingApply = true;
        appCommandSettings.UpdatedBy = adminId;
        appCommandSettings.UpdatedUtc = now;

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var functionPayload = _payloadBuilder.BuildAgentPayload(
            _payloadBuilder.MapToPayloadRequest(appCommandSettings, legacyTaskId, agentAction));

        if (functionPayload.Length > WindowsApplicationCommandModuleConstants.MaxFunctionParameterLength)
        {
            return ApplicationCommandWorkResult.Failure(
                "ValidationFailed",
                $"Agent payload exceeds {WindowsApplicationCommandModuleConstants.MaxFunctionParameterLength} characters ({functionPayload.Length}).");
        }

        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = moduleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = _payloadBuilder.BuildExtraData(
                device.MacAddress,
                string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
                    ? WindowsApplicationCommandModuleConstants.DefaultSignalSuffix
                    : _options.DefaultSignalSuffix),
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            settingsKind,
            appCommandSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (WindowsApplicationCommandModuleConstants.IsQueuedApplyFunctionName(functionName))
        {
            return ApplicationCommandWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary && _options.LegacySummaryEnabled,
                correlationId));
        }

        return ApplicationCommandWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary && _options.LegacySummaryEnabled,
            correlationId));
    }

    private static void ApplySettingsRequest(
        DeviceWindowsApplicationCommandSettings entity,
        WindowsApplicationCommandSettingsRequest request,
        int agentAction)
    {
        entity.Mode = WindowsApplicationCommandModuleConstants.ResolveModuleName(request.Mode);
        entity.ApplicationPath = request.ApplicationPath.Trim();
        entity.Parameters = request.Parameters.Trim();
        entity.WarnUser = request.WarnUser;
        entity.AlertTitle = request.AlertTitle.Trim();
        entity.AlertMessage = request.AlertMessage.Trim();
        entity.MessageType = request.MessageType.Trim();
        entity.DisplayTime = request.DisplayTime.Trim();
        entity.CommandText = request.CommandText.Trim();
        entity.Timeout = request.Timeout.Trim();
        entity.RebootRequired = request.RebootRequired;
        entity.RequireCommandOutput = request.RequireCommandOutput;
        entity.AgentAction = agentAction;
    }

    private async Task<string?> GetApplicationCommandBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return WindowsApplicationCommandApplyBlockReason.EnrollmentStateBlocked;
        }

        var moduleNames = new[]
        {
            WindowsApplicationCommandModuleConstants.ApplicationModuleName,
            WindowsApplicationCommandModuleConstants.CommandModuleName
        };

        var activeTaskStatus = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId &&
                        moduleNames.Contains(t.ModuleName) &&
                        (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess))
            .Select(t => (DeviceTaskStatus?)t.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeTaskStatus is null)
        {
            return null;
        }

        return activeTaskStatus == DeviceTaskStatus.InProcess
            ? WindowsApplicationCommandApplyBlockReason.InProcessTaskExists
            : WindowsApplicationCommandApplyBlockReason.PendingTaskExists;
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
            .Include(d => d.WindowsApplicationCommandSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private static bool IsSupportedMode(string mode) =>
        string.Equals(mode, WindowsApplicationCommandModuleConstants.ApplicationModuleName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, WindowsApplicationCommandModuleConstants.CommandModuleName, StringComparison.OrdinalIgnoreCase);

    private static WindowsApplicationCommandTargetResponse BuildTargetResponse(string macAddress) =>
        new()
        {
            MacAddress = macAddress,
            OsType = ExtractOsType(macAddress)
        };

    private static string BuildApplyLogMessage(string mode, bool instant)
    {
        var isCommand = string.Equals(
            mode,
            WindowsApplicationCommandModuleConstants.CommandModuleName,
            StringComparison.OrdinalIgnoreCase);
        var label = isCommand ? "Command" : "Application";
        return instant
            ? $"{label} instant apply queued."
            : $"{label} scheduled queue.";
    }

    private static string BuildTemplateApplyLogMessage(
        string mode,
        WindowsApplicationCommandExecutionRequest execution)
    {
        var label = string.Equals(
            mode,
            WindowsApplicationCommandModuleConstants.CommandModuleName,
            StringComparison.OrdinalIgnoreCase)
            ? "Command"
            : "Application";
        var templateName = execution.TemplateName?.Trim();
        if (execution.TemplateId is > 0 && !string.IsNullOrWhiteSpace(templateName))
        {
            return $"{label} SysView template queue ({templateName}, id {execution.TemplateId.Value}).";
        }

        if (execution.TemplateId is > 0)
        {
            return $"{label} SysView template queue (id {execution.TemplateId.Value}).";
        }

        return $"{label} SysView template queue.";
    }

    private bool ShouldReturnLegacySummary(WindowsApplicationCommandOptionsRequest options) =>
        options.ReturnLegacySummary && _options.LegacySummaryEnabled;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsType(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private async Task<WindowsApplicationCommandBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsApplicationCommandTargetRequest> uniqueTargets,
        WindowsApplicationCommandSettingsRequest settingsTemplate,
        WindowsApplicationCommandExecutionRequest execution,
        WindowsApplicationCommandOptionsRequest options,
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

            var dryRunResults = new List<WindowsApplicationCommandTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsApplicationCommandTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.TryGetValue(mac, out var device))
                {
                    dryRunResults.Add(new WindowsApplicationCommandTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                var blockReason = await GetApplicationCommandBlockReasonAsync(
                    device.Id,
                    device.EnrollmentState,
                    cancellationToken);
                if (blockReason is not null)
                {
                    dryRunResults.Add(new WindowsApplicationCommandTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = blockReason
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsApplicationCommandTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsApplicationCommandBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        var results = new List<WindowsApplicationCommandTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!IsXpDevice(mac))
            {
                blocked++;
                results.Add(new WindowsApplicationCommandTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var work = await QueueApplicationCommandWorkAsync(
                target,
                settingsTemplate,
                execution,
                options,
                adminId,
                WindowsApplicationCommandModuleConstants.InstantFunctionName,
                "instant",
                BuildApplyLogMessage(settingsTemplate.Mode, instant: true),
                cancellationToken);

            if (work.ExecuteNowResult is null)
            {
                blocked++;
                results.Add(new WindowsApplicationCommandTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = WindowsApplicationCommandApplyBlockReason.MapBulkBlockReason(
                        work.ErrorCode,
                        work.Message)
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty
                ? work.ExecuteNowResult.Response!.Data.TaskId
                : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsApplicationCommandTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        return WindowsApplicationCommandBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private static WindowsApplicationCommandBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsApplicationCommandTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsApplicationCommandBulkData
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

    private static WindowsApplicationCommandLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsApplicationCommandQueueResponse BuildQueueResponse(
        WindowsApplicationCommandTargetRequest target,
        WindowsApplicationCommandExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        var scheduleType = NormalizeScheduleType(execution.ScheduleType);
        return new WindowsApplicationCommandQueueResponse
        {
            Success = true,
            Message = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase)
                ? "Template queue accepted."
                : "Queue scheduled successfully.",
            Data = new WindowsApplicationCommandQueueData
            {
                TaskId = taskId,
                Target = new WindowsApplicationCommandTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsApplicationCommandExecutionResponse
                {
                    ScheduleType = scheduleType,
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                Template = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase) &&
                           execution.TemplateId is > 0
                    ? new WindowsApplicationCommandTemplateInfo
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

    private static WindowsApplicationCommandExecuteNowResponse BuildExecuteNowResponse(
        WindowsApplicationCommandTargetRequest target,
        WindowsApplicationCommandExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsApplicationCommandExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsApplicationCommandTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsApplicationCommandExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static string MapTaskApplyMode(string functionName) =>
        WindowsApplicationCommandModuleConstants.MapApplyMode(functionName);

    private static string MapTaskToApplyStatus(DeviceTaskStatus status) => status switch
    {
        DeviceTaskStatus.Pending => "Pending",
        DeviceTaskStatus.InProcess => "Delivered",
        DeviceTaskStatus.Completed => "Applied",
        DeviceTaskStatus.Failed => "Failed",
        _ => "Pending"
    };

    private static int ParseAgentAction(string? agentAction) =>
        int.TryParse(agentAction?.Trim(), out var value) ? value : 0;

    private static string NormalizeScheduleType(string? scheduleType)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
        {
            return "InstantApply";
        }

        return scheduleType.Trim();
    }

    private static string ExtractOsType(string macAddress)
    {
        if (string.IsNullOrWhiteSpace(macAddress))
        {
            return "XP";
        }

        var trimmed = macAddress.Trim();
        var idx = trimmed.LastIndexOf(':');
        if (idx < 0 || idx == trimmed.Length - 1)
        {
            return "XP";
        }

        return trimmed[(idx + 1)..].ToUpperInvariant();
    }

    private sealed class ApplicationCommandWorkResult
    {
        public WindowsApplicationCommandExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsApplicationCommandQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static ApplicationCommandWorkResult FromExecuteNow(WindowsApplicationCommandExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsApplicationCommandExecuteNowResult.Success(response) };

        public static ApplicationCommandWorkResult FromQueue(WindowsApplicationCommandQueueResponse response) =>
            new() { QueueResult = WindowsApplicationCommandQueueResult.Success(response) };

        public static ApplicationCommandWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }
}
