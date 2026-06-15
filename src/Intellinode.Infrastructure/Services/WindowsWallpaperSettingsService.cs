using System.Text.Json;
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

public sealed class WindowsWallpaperSettingsService : IWindowsWallpaperSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsWallpaperPayloadBuilder _payloadBuilder;
    private readonly WindowsWallpaperOptions _options;

    public WindowsWallpaperSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsWallpaperPayloadBuilder payloadBuilder,
        IOptions<WindowsWallpaperOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _options = options.Value;
    }

    public async Task<WindowsWallpaperExecuteNowResult> ExecuteNowAsync(
        WindowsWallpaperExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWallpaperExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueWallpaperWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsWallpaperModuleConstants.InstantFunctionName,
                "instant",
                "Wallpaper instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsWallpaperExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsWallpaperExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWallpaperQueueResult> QueueAsync(
        WindowsWallpaperQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWallpaperQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueWallpaperWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsWallpaperModuleConstants.QueuedFunctionName,
                "queued",
                "Wallpaper scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsWallpaperQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsWallpaperQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWallpaperQueueResult> TemplateQueueAsync(
        WindowsWallpaperTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWallpaperQueueResult.Failure(
                    "ValidationFailed",
                    "Only QueueTemplate is supported on this endpoint.");
            }

            var queueResult = await QueueWallpaperWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsWallpaperModuleConstants.TemplateQueueFunctionName,
                "template",
                BuildTemplateApplyLogMessage(request.Execution),
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsWallpaperQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Template queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsWallpaperQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWallpaperBulkResult> ExecuteNowBulkAsync(
        WindowsWallpaperExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWallpaperBulkResult.Failure(
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
            return WindowsWallpaperBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWallpaperBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsWallpaperExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWallpaperBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsWallpaperBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsWallpaperSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsWallpaperTargetRequest
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
            return WindowsWallpaperBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWallpaperCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsWallpaperCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsWallpaperCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsWallpaperSettings;
            var hasSettings = settings is not null;

            return WindowsWallpaperCurrentResult.Success(new WindowsWallpaperCurrentResponse
            {
                Success = true,
                Message = "Wallpaper settings fetched successfully.",
                Data = new WindowsWallpaperCurrentData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Settings = hasSettings
                        ? MapCurrentSettingsDto(settings!)
                        : WindowsWallpaperCurrentSettingsDto.CreateFusionXDefaults(),
                    Compat = new WindowsWallpaperCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsWallpaperCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWallpaperHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsWallpaperHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsWallpaperHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsWallpaperHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsWallpaperModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsWallpaper);

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

            var taskItems = tasks.Select(t => new WindowsWallpaperHistoryItem
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

            var logItems = logs.Select(l => new WindowsWallpaperHistoryItem
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

            return WindowsWallpaperHistoryResult.Success(new WindowsWallpaperHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsWallpaperHistoryData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = items,
                    Pagination = new WindowsWallpaperPagination
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
            return WindowsWallpaperHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static WindowsWallpaperCurrentSettingsDto MapCurrentSettingsDto(
        DeviceWindowsWallpaperSettings settings) =>
        new()
        {
            SourceType = settings.SourceType,
            PicturePath = settings.PicturePath,
            PictureName = settings.PictureName,
            PicturePosition = settings.PicturePosition,
            PreventUserChanges = settings.PreventUserChanges,
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

    private async Task<WallpaperWorkResult> QueueWallpaperWorkAsync(
        WindowsWallpaperTargetRequest target,
        WindowsWallpaperSettingsRequest settings,
        WindowsWallpaperExecutionRequest execution,
        WindowsWallpaperOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var normalizedMac = target.MacAddress.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        var validationError = WindowsWallpaperRequestValidation.ValidateRepositorySettings(settings);
        if (validationError is not null)
        {
            return WallpaperWorkResult.Failure("ValidationFailed", validationError);
        }

        if (!WindowsWallpaperRequestValidation.PayloadWithinLimit(
                settings,
                ParseAgentAction(execution.AgentAction)))
        {
            return WallpaperWorkResult.Failure(
                "ValidationFailed",
                $"Serialized agent payload exceeds {WindowsWallpaperModuleConstants.MaxFunctionParameterLength} characters.");
        }

        if (options.DryRun)
        {
            if (WindowsWallpaperModuleConstants.IsQueuedApplyFunctionName(functionName))
            {
                return WallpaperWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return WallpaperWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return WallpaperWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetWallpaperBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return WallpaperWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var wallpaper = device.WindowsWallpaperSettings;
        if (wallpaper is null)
        {
            wallpaper = new DeviceWindowsWallpaperSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsWallpaperSettings.Add(wallpaper);
            device.WindowsWallpaperSettings = wallpaper;
        }

        var agentAction = ParseAgentAction(execution.AgentAction);
        ApplySettingsRequest(wallpaper, settings, agentAction);
        wallpaper.SettingsVersion++;
        wallpaper.PendingApply = true;
        wallpaper.UpdatedBy = adminId;
        wallpaper.UpdatedUtc = now;

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        string functionPayload;
        if (IsRepositoryPath(settings))
        {
            _dbContext.DeviceWindowsWallpaperSettingsSnapshots.Add(new DeviceWindowsWallpaperSettingsSnapshot
            {
                DeviceId = device.Id,
                SettingsVersion = wallpaper.SettingsVersion,
                SourceType = wallpaper.SourceType,
                PicturePath = wallpaper.PicturePath,
                PictureName = wallpaper.PictureName,
                PicturePosition = wallpaper.PicturePosition,
                PreventUserChanges = wallpaper.PreventUserChanges,
                Upload = wallpaper.Upload,
                AgentAction = agentAction,
                RepositoryJson = wallpaper.RepositoryJson,
                CreatedUtc = now
            });

            functionPayload = _payloadBuilder.BuildCompactTaskReference(wallpaper.SettingsVersion);
        }
        else
        {
            functionPayload = _payloadBuilder.BuildAgentPayload(
                _payloadBuilder.MapToPayloadRequest(wallpaper, legacyTaskId, agentAction));

            if (functionPayload.Length > WindowsWallpaperModuleConstants.MaxFunctionParameterLength)
            {
                return WallpaperWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsWallpaperModuleConstants.MaxFunctionParameterLength} characters ({functionPayload.Length}).");
            }
        }

        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = WindowsWallpaperModuleConstants.ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = _payloadBuilder.BuildExtraData(
                device.MacAddress,
                string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
                    ? WindowsWallpaperModuleConstants.DefaultSignalSuffix
                    : _options.DefaultSignalSuffix),
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.WindowsWallpaper,
            wallpaper.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (WindowsWallpaperModuleConstants.IsQueuedApplyFunctionName(functionName))
        {
            return WallpaperWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary && _options.LegacySummaryEnabled,
                correlationId));
        }

        return WallpaperWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary && _options.LegacySummaryEnabled,
            correlationId));
    }

    private static void ApplySettingsRequest(
        DeviceWindowsWallpaperSettings entity,
        WindowsWallpaperSettingsRequest request,
        int agentAction)
    {
        entity.SourceType = NormalizeSourceType(request.SourceType, request.Upload);
        entity.Upload = request.Upload || IsRepositorySourceType(entity.SourceType);
        entity.PicturePosition = request.PicturePosition.Trim();
        entity.PreventUserChanges = request.PreventUserChanges;
        entity.AgentAction = agentAction;

        if (IsRepositoryPath(request))
        {
            entity.PictureName = request.PictureName.Trim();
            entity.PicturePath = string.Empty;
            entity.RepositoryJson = SerializeRepositoryJson(request.Repository!);
        }
        else
        {
            entity.PicturePath = request.PicturePath.Trim();
            entity.PictureName = string.Empty;
            entity.Upload = false;
            entity.SourceType = "Browse";
            entity.RepositoryJson = null;
        }
    }

    internal static bool IsRepositoryPath(WindowsWallpaperSettingsRequest settings) =>
        WindowsWallpaperRequestValidation.IsRepositoryPath(settings);

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

    private static string SerializeRepositoryJson(WindowsWallpaperRepositoryRequest repository) =>
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

    private async Task<string?> GetWallpaperBlockReasonAsync(
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
                     t.ModuleName == WindowsWallpaperModuleConstants.ModuleName &&
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
            .Include(d => d.WindowsWallpaperSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private static WindowsWallpaperTargetResponse BuildTargetResponse(string macAddress) =>
        new()
        {
            MacAddress = macAddress,
            OsType = ExtractOsType(macAddress)
        };

    private static string BuildTemplateApplyLogMessage(WindowsWallpaperExecutionRequest execution)
    {
        var templateName = execution.TemplateName?.Trim();
        if (execution.TemplateId is > 0 && !string.IsNullOrWhiteSpace(templateName))
        {
            return $"Wallpaper SysView template queue ({templateName}, id {execution.TemplateId.Value}).";
        }

        if (execution.TemplateId is > 0)
        {
            return $"Wallpaper SysView template queue (id {execution.TemplateId.Value}).";
        }

        return "Wallpaper SysView template queue.";
    }

    private bool ShouldReturnLegacySummary(WindowsWallpaperOptionsRequest options) =>
        options.ReturnLegacySummary && _options.LegacySummaryEnabled;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private async Task<WindowsWallpaperBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsWallpaperTargetRequest> uniqueTargets,
        WindowsWallpaperSettingsRequest settingsTemplate,
        WindowsWallpaperExecutionRequest execution,
        WindowsWallpaperOptionsRequest options,
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

            var dryRunResults = new List<WindowsWallpaperTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsWallpaperTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.ContainsKey(mac))
                {
                    dryRunResults.Add(new WindowsWallpaperTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsWallpaperTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsWallpaperBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        var results = new List<WindowsWallpaperTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!IsXpDevice(mac))
            {
                blocked++;
                results.Add(new WindowsWallpaperTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var work = await QueueWallpaperWorkAsync(
                target,
                settingsTemplate,
                execution,
                options,
                adminId,
                WindowsWallpaperModuleConstants.InstantFunctionName,
                "instant",
                "Wallpaper bulk instant apply queued.",
                cancellationToken);

            if (work.ExecuteNowResult is null)
            {
                blocked++;
                results.Add(new WindowsWallpaperTargetResult
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
            results.Add(new WindowsWallpaperTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        return WindowsWallpaperBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private static WindowsWallpaperBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsWallpaperTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsWallpaperBulkData
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
        WindowsWallpaperModuleConstants.MapApplyMode(functionName);

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
        var colonIndex = macAddress.LastIndexOf(':');
        return colonIndex >= 0 && colonIndex < macAddress.Length - 1
            ? macAddress[(colonIndex + 1)..]
            : string.Empty;
    }

    private static string ExtractOsSuffix(string macAddress)
    {
        var trimmed = macAddress.Trim();
        var idx = trimmed.LastIndexOf(':');
        if (idx < 0 || idx >= trimmed.Length - 1)
        {
            return string.Empty;
        }

        return trimmed[(idx + 1)..].ToUpperInvariant();
    }

    private static WindowsWallpaperLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsWallpaperQueueResponse BuildQueueResponse(
        WindowsWallpaperTargetRequest target,
        WindowsWallpaperExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        var scheduleType = NormalizeScheduleType(execution.ScheduleType);
        return new WindowsWallpaperQueueResponse
        {
            Success = true,
            Message = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase)
                ? "Template queue accepted."
                : "Queue accepted.",
            Data = new WindowsWallpaperQueueData
            {
                TaskId = taskId,
                Target = new WindowsWallpaperTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsWallpaperExecutionResponse
                {
                    ScheduleType = scheduleType,
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                Template = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase) &&
                           execution.TemplateId is > 0
                    ? new WindowsWallpaperTemplateInfo
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

    private static WindowsWallpaperExecuteNowResponse BuildExecuteNowResponse(
        WindowsWallpaperTargetRequest target,
        WindowsWallpaperExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsWallpaperExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsWallpaperTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsWallpaperExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private sealed class WallpaperWorkResult
    {
        public WindowsWallpaperExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsWallpaperQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static WallpaperWorkResult FromExecuteNow(WindowsWallpaperExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsWallpaperExecuteNowResult.Success(response) };

        public static WallpaperWorkResult FromQueue(WindowsWallpaperQueueResponse response) =>
            new() { QueueResult = WindowsWallpaperQueueResult.Success(response) };

        public static WallpaperWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }
}
