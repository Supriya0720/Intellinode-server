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

public sealed class WindowsUserInterfaceSettingsService : IWindowsUserInterfaceSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly IWindowsUserInterfacePayloadBuilder _payloadBuilder;
    private readonly IWindowsUserInterfacePasswordProtector _passwordProtector;
    private readonly WindowsUserInterfaceOptions _options;

    public WindowsUserInterfaceSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IWindowsUserInterfacePayloadBuilder payloadBuilder,
        IWindowsUserInterfacePasswordProtector passwordProtector,
        IOptions<WindowsUserInterfaceOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _payloadBuilder = payloadBuilder;
        _passwordProtector = passwordProtector;
        _options = options.Value;
    }

    public async Task<WindowsUserInterfaceExecuteNowResult> ExecuteNowAsync(
        WindowsUserInterfaceExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsUserInterfaceExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueUserInterfaceWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsUserInterfaceModuleConstants.InstantFunctionName,
                "instant",
                "Autologon instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? WindowsUserInterfaceExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsUserInterfaceExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsUserInterfaceQueueResult> QueueAsync(
        WindowsUserInterfaceQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsUserInterfaceQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueUserInterfaceWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsUserInterfaceModuleConstants.QueuedFunctionName,
                "queued",
                "Autologon scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsUserInterfaceQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsUserInterfaceQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsUserInterfaceCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsUserInterfaceCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsUserInterfaceCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.WindowsUserInterfaceSettings;
            var hasSettings = settings is not null;

            return WindowsUserInterfaceCurrentResult.Success(new WindowsUserInterfaceCurrentResponse
            {
                Success = true,
                Message = "User interface settings fetched successfully.",
                Data = new WindowsUserInterfaceCurrentData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Settings = hasSettings
                        ? MapCurrentSettingsDto(settings!)
                        : WindowsUserInterfaceCurrentSettingsDto.CreateFusionXDefaults(),
                    Compat = new WindowsUserInterfaceCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsUserInterfaceCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsUserInterfaceUsersResult> GetUsersAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsUserInterfaceUsersResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsUserInterfaceUsersResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddUserNameCandidate(names, device.UserName);
            AddUserNameCandidate(names, device.LoginUserName);
            if (device.WindowsUserInterfaceSettings is not null)
            {
                AddUserNameCandidate(names, device.WindowsUserInterfaceSettings.UserName);
            }

            var items = names
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => new WindowsUserInterfaceUserItemDto { UserName = n })
                .ToList();

            return WindowsUserInterfaceUsersResult.Success(new WindowsUserInterfaceUsersResponse
            {
                Success = true,
                Message = items.Count > 0
                    ? "User list returned from device metadata."
                    : "User list stub returned. Agent user enumeration integration is pending.",
                Data = new WindowsUserInterfaceUsersData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = items,
                    Compat = new WindowsUserInterfaceUsersCompatDto
                    {
                        Source = items.Count > 0 ? "device" : "stub"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsUserInterfaceUsersResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsUserInterfaceHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsUserInterfaceHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsUserInterfaceHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsUserInterfaceHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = WindowsUserInterfaceModuleConstants.ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsUserInterface);

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

            var taskItems = tasks.Select(t => new WindowsUserInterfaceHistoryItem
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

            var logItems = logs.Select(l => new WindowsUserInterfaceHistoryItem
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

            return WindowsUserInterfaceHistoryResult.Success(new WindowsUserInterfaceHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsUserInterfaceHistoryData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = items,
                    Pagination = new WindowsUserInterfacePagination
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
            return WindowsUserInterfaceHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsUserInterfaceQueueResult> TemplateQueueAsync(
        WindowsUserInterfaceTemplateQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "QueueTemplate", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsUserInterfaceQueueResult.Failure(
                    "ValidationFailed",
                    "Only QueueTemplate is supported on this endpoint.");
            }

            var queueResult = await QueueUserInterfaceWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                WindowsUserInterfaceModuleConstants.TemplateQueueFunctionName,
                "template",
                BuildTemplateApplyLogMessage(request.Execution),
                cancellationToken);

            return queueResult.QueueResult
                ?? WindowsUserInterfaceQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Template queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsUserInterfaceQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsUserInterfaceBulkResult> ExecuteNowBulkAsync(
        WindowsUserInterfaceExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsUserInterfaceBulkResult.Failure(
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
            return WindowsUserInterfaceBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsUserInterfaceBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsUserInterfaceExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsUserInterfaceBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsUserInterfaceBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsUserInterfaceSettings)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsUserInterfaceTargetRequest
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
            return WindowsUserInterfaceBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static WindowsUserInterfaceCurrentSettingsDto MapCurrentSettingsDto(
        DeviceWindowsUserInterfaceSettings settings) =>
        new()
        {
            UserName = settings.UserName,
            AutoLogon = settings.AutoLogon,
            HasPassword = !string.IsNullOrWhiteSpace(settings.PasswordCipher),
            AgentAction = settings.AgentAction,
            SettingsVersion = settings.SettingsVersion,
            PendingApply = settings.PendingApply,
            LastAppliedVersion = settings.LastAppliedVersion,
            LastAppliedUtc = settings.LastAppliedUtc,
            LastApplyStatus = settings.LastApplyStatus,
            LastApplyMessage = settings.LastApplyMessage
        };

    private async Task<UserInterfaceWorkResult> QueueUserInterfaceWorkAsync(
        WindowsUserInterfaceTargetRequest target,
        WindowsUserInterfaceSettingsRequest settings,
        WindowsUserInterfaceExecutionRequest execution,
        WindowsUserInterfaceOptionsRequest options,
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
        var useCompactReference = WindowsUserInterfaceModuleConstants.IsQueuedApplyFunctionName(functionName);

        var credentialValidationError = WindowsUserInterfaceRequestValidation.ValidateAutologonCredentials(settings);
        if (credentialValidationError is not null)
        {
            return UserInterfaceWorkResult.Failure("ValidationFailed", credentialValidationError);
        }

        if (!WindowsUserInterfaceRequestValidation.PayloadWithinLimit(
                settings,
                agentAction,
                useCompactReference))
        {
            return UserInterfaceWorkResult.Failure(
                "ValidationFailed",
                $"Serialized agent payload exceeds {WindowsUserInterfaceModuleConstants.MaxFunctionParameterLength} characters.");
        }

        if (options.DryRun)
        {
            if (WindowsUserInterfaceModuleConstants.IsQueuedApplyFunctionName(functionName))
            {
                return UserInterfaceWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return UserInterfaceWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return UserInterfaceWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetUserInterfaceBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return UserInterfaceWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var uiSettings = device.WindowsUserInterfaceSettings;
        if (uiSettings is null)
        {
            uiSettings = new DeviceWindowsUserInterfaceSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsUserInterfaceSettings.Add(uiSettings);
            device.WindowsUserInterfaceSettings = uiSettings;
        }

        var applyPasswordResult = ApplySettingsRequest(uiSettings, settings, agentAction);
        if (applyPasswordResult.Error is not null)
        {
            return UserInterfaceWorkResult.Failure("ValidationFailed", applyPasswordResult.Error);
        }

        uiSettings.SettingsVersion++;
        uiSettings.PendingApply = true;
        uiSettings.UpdatedBy = adminId;
        uiSettings.UpdatedUtc = now;

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        string functionPayload;

        if (useCompactReference)
        {
            _dbContext.DeviceWindowsUserInterfaceSettingsSnapshots.Add(new DeviceWindowsUserInterfaceSettingsSnapshot
            {
                DeviceId = device.Id,
                SettingsVersion = uiSettings.SettingsVersion,
                UserName = uiSettings.UserName,
                AutoLogon = uiSettings.AutoLogon,
                PasswordCipher = uiSettings.PasswordCipher,
                AgentAction = agentAction,
                CreatedUtc = now
            });

            functionPayload = _payloadBuilder.BuildCompactTaskReference(uiSettings.SettingsVersion);
        }
        else
        {
            functionPayload = _payloadBuilder.BuildAgentPayload(
                _payloadBuilder.MapToPayloadRequest(
                    uiSettings,
                    legacyTaskId,
                    agentAction,
                    applyPasswordResult.PlaintextPassword ?? string.Empty));

            if (functionPayload.Length > WindowsUserInterfaceModuleConstants.MaxFunctionParameterLength)
            {
                return UserInterfaceWorkResult.Failure(
                    "ValidationFailed",
                    $"Agent payload exceeds {WindowsUserInterfaceModuleConstants.MaxFunctionParameterLength} characters ({functionPayload.Length}).");
            }
        }

        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = WindowsUserInterfaceModuleConstants.ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = _payloadBuilder.BuildExtraData(
                device.MacAddress,
                string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
                    ? WindowsUserInterfaceModuleConstants.DefaultSignalSuffix
                    : _options.DefaultSignalSuffix),
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.WindowsUserInterface,
            uiSettings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (WindowsUserInterfaceModuleConstants.IsQueuedApplyFunctionName(functionName))
        {
            return UserInterfaceWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary && _options.LegacySummaryEnabled,
                correlationId));
        }

        return UserInterfaceWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary && _options.LegacySummaryEnabled,
            correlationId));
    }

    private ApplyPasswordResult ApplySettingsRequest(
        DeviceWindowsUserInterfaceSettings entity,
        WindowsUserInterfaceSettingsRequest request,
        int agentAction)
    {
        entity.UserName = request.UserName.Trim();
        entity.AutoLogon = request.AutoLogon;
        entity.AgentAction = agentAction;

        if (!request.AutoLogon)
        {
            entity.PasswordCipher = null;
            return ApplyPasswordResult.Success(null);
        }

        if (request.KeepExistingPassword)
        {
            if (string.IsNullOrWhiteSpace(entity.PasswordCipher))
            {
                return ApplyPasswordResult.Failure("No stored password to retain.");
            }

            if (!_passwordProtector.TryUnprotect(entity.PasswordCipher, out var existingPassword))
            {
                return ApplyPasswordResult.Failure("Stored password could not be decrypted.");
            }

            return ApplyPasswordResult.Success(existingPassword);
        }

        var password = request.Password ?? string.Empty;
        entity.PasswordCipher = _passwordProtector.Protect(password);
        return ApplyPasswordResult.Success(password);
    }

    private async Task<string?> GetUserInterfaceBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return WindowsUserInterfaceApplyBlockReason.EnrollmentStateBlocked;
        }

        var activeTaskStatus = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId &&
                        t.ModuleName == WindowsUserInterfaceModuleConstants.ModuleName &&
                        (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess))
            .Select(t => (DeviceTaskStatus?)t.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeTaskStatus is null)
        {
            return null;
        }

        return activeTaskStatus == DeviceTaskStatus.InProcess
            ? WindowsUserInterfaceApplyBlockReason.InProcessTaskExists
            : WindowsUserInterfaceApplyBlockReason.PendingTaskExists;
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
            .Include(d => d.WindowsUserInterfaceSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private static void AddUserNameCandidate(ISet<string> names, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var trimmed = candidate.Trim();
        if (string.Equals(trimmed, "---Select---", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        names.Add(trimmed);
    }

    private static WindowsUserInterfaceTargetResponse BuildTargetResponse(string macAddress) =>
        new()
        {
            MacAddress = macAddress,
            OsType = ExtractOsType(macAddress)
        };

    private static string MapTaskApplyMode(string functionName) =>
        WindowsUserInterfaceModuleConstants.MapApplyMode(functionName);

    private static string MapTaskToApplyStatus(DeviceTaskStatus status) => status switch
    {
        DeviceTaskStatus.Pending => "Pending",
        DeviceTaskStatus.InProcess => "Delivered",
        DeviceTaskStatus.Completed => "Applied",
        DeviceTaskStatus.Failed => "Failed",
        _ => "Pending"
    };

    internal static string NormalizeScheduleType(string? scheduleType)
    {
        if (string.IsNullOrWhiteSpace(scheduleType))
        {
            return "InstantApply";
        }

        return scheduleType.Trim();
    }

    internal static int ParseAgentAction(string? agentAction)
    {
        if (string.IsNullOrWhiteSpace(agentAction))
        {
            return 0;
        }

        return int.TryParse(agentAction.Trim(), out var value) ? value : 0;
    }

    internal static string ExtractOsType(string macAddress)
    {
        var suffix = ExtractOsSuffix(macAddress);
        return suffix ?? "XP";
    }

    internal static string? ExtractOsSuffix(string macAddress)
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

    private static string BuildTemplateApplyLogMessage(WindowsUserInterfaceExecutionRequest execution)
    {
        var templateName = execution.TemplateName?.Trim();
        if (execution.TemplateId is > 0 && !string.IsNullOrWhiteSpace(templateName))
        {
            return $"Autologon SysView template queue ({templateName}, id {execution.TemplateId.Value}).";
        }

        if (execution.TemplateId is > 0)
        {
            return $"Autologon SysView template queue (id {execution.TemplateId.Value}).";
        }

        return "Autologon SysView template queue.";
    }

    private bool ShouldReturnLegacySummary(WindowsUserInterfaceOptionsRequest options) =>
        options.ReturnLegacySummary && _options.LegacySummaryEnabled;

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private async Task<WindowsUserInterfaceBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsUserInterfaceTargetRequest> uniqueTargets,
        WindowsUserInterfaceSettingsRequest settingsTemplate,
        WindowsUserInterfaceExecutionRequest execution,
        WindowsUserInterfaceOptionsRequest options,
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

            var dryRunResults = new List<WindowsUserInterfaceTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsUserInterfaceTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                if (!dryRunByMac.TryGetValue(mac, out var device))
                {
                    dryRunResults.Add(new WindowsUserInterfaceTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "DeviceNotFound"
                    });
                    continue;
                }

                var blockReason = await GetUserInterfaceBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
                if (blockReason is not null)
                {
                    dryRunResults.Add(new WindowsUserInterfaceTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = blockReason
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsUserInterfaceTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending"
                });
            }

            return WindowsUserInterfaceBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                ShouldReturnLegacySummary(options),
                correlationId));
        }

        var results = new List<WindowsUserInterfaceTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!IsXpDevice(mac))
            {
                blocked++;
                results.Add(new WindowsUserInterfaceTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var work = await QueueUserInterfaceWorkAsync(
                target,
                settingsTemplate,
                execution,
                options,
                adminId,
                WindowsUserInterfaceModuleConstants.InstantFunctionName,
                "instant",
                "Autologon bulk instant apply queued.",
                cancellationToken);

            if (work.ExecuteNowResult is null)
            {
                blocked++;
                results.Add(new WindowsUserInterfaceTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = WindowsUserInterfaceApplyBlockReason.MapBulkBlockReason(work.ErrorCode, work.Message)
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty
                ? work.ExecuteNowResult.Response!.Data.TaskId
                : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsUserInterfaceTargetResult
            {
                MacAddress = mac,
                Status = "Pending"
            });
        }

        return WindowsUserInterfaceBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            ShouldReturnLegacySummary(options),
            correlationId));
    }

    private static WindowsUserInterfaceBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsUserInterfaceTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsUserInterfaceBulkData
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

    internal static WindowsUserInterfaceLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    internal static WindowsUserInterfaceQueueResponse BuildQueueResponse(
        WindowsUserInterfaceTargetRequest target,
        WindowsUserInterfaceExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        var scheduleType = NormalizeScheduleType(execution.ScheduleType);
        return new WindowsUserInterfaceQueueResponse
        {
            Success = true,
            Message = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase)
                ? "Template queue accepted."
                : "Queue accepted.",
            Data = new WindowsUserInterfaceQueueData
            {
                TaskId = taskId,
                Target = new WindowsUserInterfaceTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsUserInterfaceExecutionResponse
                {
                    ScheduleType = scheduleType,
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                Template = string.Equals(scheduleType, "QueueTemplate", StringComparison.OrdinalIgnoreCase) &&
                           execution.TemplateId is > 0
                    ? new WindowsUserInterfaceTemplateInfo
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

    internal static WindowsUserInterfaceExecuteNowResponse BuildExecuteNowResponse(
        WindowsUserInterfaceTargetRequest target,
        WindowsUserInterfaceExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsUserInterfaceExecuteNowData
            {
                TaskId = taskId,
                Target = new WindowsUserInterfaceTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new WindowsUserInterfaceExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private readonly struct ApplyPasswordResult
    {
        public string? Error { get; init; }
        public string? PlaintextPassword { get; init; }

        public static ApplyPasswordResult Success(string? plaintextPassword) =>
            new() { PlaintextPassword = plaintextPassword };

        public static ApplyPasswordResult Failure(string error) =>
            new() { Error = error };
    }

    internal sealed class UserInterfaceWorkResult
    {
        public WindowsUserInterfaceExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsUserInterfaceQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static UserInterfaceWorkResult FromExecuteNow(WindowsUserInterfaceExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsUserInterfaceExecuteNowResult.Success(response) };

        public static UserInterfaceWorkResult FromQueue(WindowsUserInterfaceQueueResponse response) =>
            new() { QueueResult = WindowsUserInterfaceQueueResult.Success(response) };

        public static UserInterfaceWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }
}
