using System.Text.Json;
using Intellinode.Application.Contracts.Admin;
using Intellinode.Application.Validation;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class WindowsScreenSaverSettingsService : IWindowsScreenSaverSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsScreenSaverPayloadBuilder _payloadBuilder;
    private readonly WindowsScreenSaverOptions _options;

    public WindowsScreenSaverSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsScreenSaverPayloadBuilder payloadBuilder,
        IOptions<WindowsScreenSaverOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
    }

    public async Task<WindowsScreenSaverExecuteNowResult> ExecuteNowAsync(
        WindowsScreenSaverExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueScreenSaverWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsScreenSaverModuleConstants.InstantFunctionName,
                "instant",
                "Screen saver instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsScreenSaverExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverQueueResult> QueueAsync(
        WindowsScreenSaverQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueScreenSaverWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsScreenSaverModuleConstants.QueuedFunctionName,
                "queued",
                "Screen saver scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsScreenSaverQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsScreenSaverCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsScreenSaverCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsScreenSaverSettings;
            var hasSettings = settings is not null;

            return WindowsScreenSaverCurrentResult.Success(new WindowsScreenSaverCurrentResponse
            {
                Success = true,
                Message = "Screen saver settings fetched successfully.",
                Data = new WindowsScreenSaverCurrentData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Settings = hasSettings
                        ? MapCurrentSettingsDto(settings!)
                        : new WindowsScreenSaverCurrentSettingsDto(),
                    Compat = new WindowsScreenSaverCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverCatalogResult> GetCatalogAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsScreenSaverCatalogResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsScreenSaverCatalogResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            return WindowsScreenSaverCatalogResult.Success(new WindowsScreenSaverCatalogResponse
            {
                Success = true,
                Message = "Screen saver catalog stub returned. Agent inventory integration is pending.",
                Data = new WindowsScreenSaverCatalogData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = [],
                    Compat = new WindowsScreenSaverCatalogCompatDto
                    {
                        Source = "stub"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverCatalogResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsScreenSaverHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsScreenSaverHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsScreenSaverHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsScreenSaverModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsScreenSaver);

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

            var taskItems = tasks.Select(t => new WindowsScreenSaverHistoryItem
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

            var logItems = logs.Select(l => new WindowsScreenSaverHistoryItem
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

            return WindowsScreenSaverHistoryResult.Success(new WindowsScreenSaverHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsScreenSaverHistoryData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = items,
                    Pagination = new WindowsScreenSaverPagination
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
            return WindowsScreenSaverHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverQueueResult> TemplateQueueAsync(
        WindowsScreenSaverTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverQueueResult.Failure(
                    "ValidationFailed",
                    "Only QueueTemplate is supported on this endpoint.");
            }

            var queueResult = await QueueScreenSaverWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsScreenSaverModuleConstants.TemplateQueueFunctionName,
                "template",
                BuildTemplateApplyLogMessage(request.Execution),
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsScreenSaverQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Template queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsScreenSaverQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverBulkResult> ExecuteNowBulkAsync(
        WindowsScreenSaverExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverBulkResult.Failure(
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
            return WindowsScreenSaverBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsScreenSaverBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsScreenSaverExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsScreenSaverBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsScreenSaverBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsScreenSaverSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsScreenSaverTargetRequest
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
            return WindowsScreenSaverBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static WindowsScreenSaverCurrentSettingsDto MapCurrentSettingsDto(
        DeviceWindowsScreenSaverSettings settings) =>
        new()
        {
            ScreenSaverName = settings.ScreenSaverName,
            TimeoutMinutes = settings.TimeoutMinutes,
            PasswordProtected = settings.PasswordProtected,
            PreventUserChanges = settings.PreventUserChanges,
            SourceType = settings.SourceType,
            Upload = settings.Upload,
            AgentAction = settings.AgentAction,
            HasRepositoryMetadata = !string.IsNullOrWhiteSpace(settings.RepositoryJson),
            SettingsVersion = settings.SettingsVersion,
            PendingApply = settings.PendingApply,
            LastAppliedVersion = settings.LastAppliedVersion,
            LastAppliedUtc = settings.LastAppliedUtc,
            LastApplyStatus = settings.LastApplyStatus,
            LastApplyMessage = settings.LastApplyMessage
        };

    private async Task<ScreenSaverWorkResult> QueueScreenSaverWorkAsync(
        WindowsScreenSaverTargetRequest target,
        WindowsScreenSaverSettingsRequest settings,
        WindowsScreenSaverExecutionRequest execution,
        WindowsScreenSaverOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var normalizedMac = target.MacAddress.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        var repositoryValidationError = WindowsScreenSaverRequestValidation.ValidateRepositorySettings(settings);
        if (repositoryValidationError is not null)
        {
            return ScreenSaverWorkResult.Failure("ValidationFailed", repositoryValidationError);
        }

        if (!WindowsScreenSaverRequestValidation.PayloadWithinLimit(
                settings,
                ParseAgentAction(execution.AgentAction)))
        {
            return ScreenSaverWorkResult.Failure(
                "ValidationFailed",
                $"Serialized agent payload exceeds {WindowsScreenSaverModuleConstants.MaxFunctionParameterLength} characters.");
        }

        if (options.DryRun)
        {
            if (WindowsScreenSaverModuleConstants.IsQueuedApplyFunctionName(functionName))
            {
                return ScreenSaverWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return ScreenSaverWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return ScreenSaverWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetScreenSaverBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return ScreenSaverWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var screenSaver = device.WindowsScreenSaverSettings;
        if (screenSaver is null)
        {
            screenSaver = new DeviceWindowsScreenSaverSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsScreenSaverSettings.Add(screenSaver);
            device.WindowsScreenSaverSettings = screenSaver;
        }

        var agentAction = ParseAgentAction(execution.AgentAction);
        ApplySettingsRequest(screenSaver, settings, agentAction);
        screenSaver.SettingsVersion++;
        screenSaver.PendingApply = true;
        screenSaver.UpdatedBy = adminId;
        screenSaver.UpdatedUtc = now;

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        string functionPayload;
        if (IsRepositoryPath(settings))
        {
            _dbContext.DeviceWindowsScreenSaverSettingsSnapshots.Add(new DeviceWindowsScreenSaverSettingsSnapshot
            {
                DeviceId = device.Id,
                SettingsVersion = screenSaver.SettingsVersion,
                ScreenSaverName = screenSaver.ScreenSaverName,
                TimeoutMinutes = screenSaver.TimeoutMinutes,
                PasswordProtected = screenSaver.PasswordProtected,
                PreventUserChanges = screenSaver.PreventUserChanges,
                SourceType = screenSaver.SourceType,
                Upload = screenSaver.Upload,
                AgentAction = agentAction,
                RepositoryJson = screenSaver.RepositoryJson,
                CreatedUtc = now
            });

            functionPayload = _payloadBuilder.BuildCompactTaskReference(screenSaver.SettingsVersion);
        }
        else
        {
            functionPayload = _payloadBuilder.BuildAgentPayload(
                _payloadBuilder.MapToPayloadRequest(screenSaver, legacyTaskId, agentAction));

            if (functionPayload.Length > WindowsScreenSaverModuleConstants.MaxFunctionParameterLength)
            {
                return ScreenSaverWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsScreenSaverModuleConstants.MaxFunctionParameterLength} characters ({functionPayload.Length}).");
            }
        }

        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = WindowsScreenSaverModuleConstants.ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = _payloadBuilder.BuildExtraData(
                device.MacAddress,
                string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
                    ? WindowsScreenSaverModuleConstants.DefaultSignalSuffix
                    : _options.DefaultSignalSuffix),
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.WindowsScreenSaver,
            screenSaver.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (WindowsScreenSaverModuleConstants.IsQueuedApplyFunctionName(functionName))
        {
            return ScreenSaverWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary && _options.LegacySummaryEnabled,
                correlationId));
        }

        return ScreenSaverWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary && _options.LegacySummaryEnabled,
            correlationId));
    }

    private static void ApplySettingsRequest(
        DeviceWindowsScreenSaverSettings entity,
        WindowsScreenSaverSettingsRequest request,
        int agentAction)
    {
        entity.ScreenSaverName = request.ScreenSaverName.Trim();
        entity.TimeoutMinutes = request.TimeoutMinutes;
        entity.PasswordProtected = request.PasswordProtected;
        entity.PreventUserChanges = request.PreventUserChanges;
        entity.SourceType = NormalizeSourceType(request.SourceType, request.Upload);
        entity.Upload = request.Upload || IsRepositorySourceType(entity.SourceType);
        entity.AgentAction = agentAction;
        entity.RepositoryJson = IsRepositoryPath(request)
            ? SerializeRepositoryJson(request.Repository!)
            : null;
    }

    internal static bool IsRepositoryPath(WindowsScreenSaverSettingsRequest settings)
    {
        var sourceType = NormalizeSourceType(settings.SourceType, settings.Upload);
        return settings.Upload
               || IsRepositorySourceType(sourceType);
    }

    private static bool IsRepositorySourceType(string sourceType) =>
        string.Equals(sourceType, "Repository", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceType, "Upload", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSourceType(string? sourceType, bool upload)
    {
        if (upload)
        {
            return "Upload";
        }

        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return "Browse";
        }

        var normalized = sourceType.Trim();
        if (string.Equals(normalized, "Repository", StringComparison.OrdinalIgnoreCase))
        {
            return "Repository";
        }

        if (string.Equals(normalized, "Upload", StringComparison.OrdinalIgnoreCase))
        {
            return "Upload";
        }

        return "Browse";
    }

    private static string SerializeRepositoryJson(WindowsScreenSaverRepositoryRequest repository) =>
        JsonSerializer.Serialize(new
        {
            connectionId = repository.ConnectionId,
            downloadIp = repository.DownloadIp.Trim(),
            ftpFolderPath = repository.FtpFolderPath.Trim(),
            ftpPassword = repository.FtpPassword,
            ftpSslType = repository.FtpSslType.Trim(),
            ftpUsername = repository.FtpUsername.Trim(),
            loggedInUserId = repository.LoggedInUserId,
            port = repository.Port,
            protocolType = repository.ProtocolType.Trim(),
            connectionName = repository.ConnectionName.Trim(),
            domainNameForRepository = repository.DomainNameForRepository.Trim()
        });

    private async Task<string?> GetScreenSaverBlockReasonAsync(
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
                     t.ModuleName == WindowsScreenSaverModuleConstants.ModuleName &&
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
            .Include(d => d.WindowsScreenSaverSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private static WindowsScreenSaverTargetResponse BuildTargetResponse(string macAddress) =>
        new()
        {
            MacAddress = macAddress,
            OsType = ExtractOsType(macAddress)
        };

    private static string BuildTemplateApplyLogMessage(WindowsScreenSaverExecutionRequest execution)
    {
        var templateName = execution.TemplateName?.Trim();
        if (execution.TemplateId is > 0 && !string.IsNullOrWhiteSpace(templateName))
        {
            return $"Screen saver SysView template queue ({templateName}, id {execution.TemplateId.Value}).";
        }

        if (execution.TemplateId is > 0)
        {
            return $"Screen saver SysView template queue (id {execution.TemplateId.Value}).";
        }

        return "Screen saver SysView template queue.";
    }

    private bool ShouldReturnLegacySummary(WindowsScreenSaverOptionsRequest options) =>
        options.ReturnLegacySummary && _options.LegacySummaryEnabled;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private async Task<WindowsScreenSaverBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsScreenSaverTargetRequest> uniqueTargets,
        WindowsScreenSaverSettingsRequest settingsTemplate,
        WindowsScreenSaverExecutionRequest execution,
        WindowsScreenSaverOptionsRequest options,
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

            var dryRunResults = new List<WindowsScreenSaverTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsScreenSaverTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsScreenSaverTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsScreenSaverTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsScreenSaverBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        var results = new List<WindowsScreenSaverTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!IsXpDevice(mac))
            {
                blocked++;
                results.Add(new WindowsScreenSaverTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var work = await QueueScreenSaverWorkAsync(
                target,
                settingsTemplate,
                execution,
                options,
                adminId,
                WindowsScreenSaverModuleConstants.InstantFunctionName,
                "instant",
                "Screen saver bulk instant apply queued.",
                cancellationToken);

            if (work.ExecuteNowResult is null)
            {
                blocked++;
                results.Add(new WindowsScreenSaverTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = work.ErrorCode ?? work.Message ?? "ApplyBlocked"
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty
                ? work.ExecuteNowResult.Response!.Data.TaskId
                : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsScreenSaverTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        return WindowsScreenSaverBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private static WindowsScreenSaverBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsScreenSaverTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsScreenSaverBulkData
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

    private static string MapTaskApplyMode(string functionName) =>
        WindowsScreenSaverModuleConstants.MapApplyMode(functionName);

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

    private static WindowsScreenSaverLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsScreenSaverQueueResponse BuildQueueResponse(
        WindowsScreenSaverTargetRequest target,
        WindowsScreenSaverExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        var scheduleType = NormalizeScheduleType(execution.ScheduleType);
        return new WindowsScreenSaverQueueResponse
        {
            Success = true,
            Message = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase)
                ? "Template queue accepted."
                : "Queue accepted.",
            Data = new WindowsScreenSaverQueueData
            {
                TaskId = taskId,
                Target = new WindowsScreenSaverTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsScreenSaverExecutionResponse
                {
                    ScheduleType = scheduleType,
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                Template = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase) &&
                           execution.TemplateId is > 0
                    ? new WindowsScreenSaverTemplateInfo
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

    private static WindowsScreenSaverExecuteNowResponse BuildExecuteNowResponse(
        WindowsScreenSaverTargetRequest target,
        WindowsScreenSaverExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsScreenSaverExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsScreenSaverTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsScreenSaverExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private sealed class ScreenSaverWorkResult
    {
        public WindowsScreenSaverExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsScreenSaverQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static ScreenSaverWorkResult FromExecuteNow(WindowsScreenSaverExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsScreenSaverExecuteNowResult.Success(response) };

        public static ScreenSaverWorkResult FromQueue(WindowsScreenSaverQueueResponse response) =>
            new() { QueueResult = WindowsScreenSaverQueueResult.Success(response) };

        public static ScreenSaverWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }
}
