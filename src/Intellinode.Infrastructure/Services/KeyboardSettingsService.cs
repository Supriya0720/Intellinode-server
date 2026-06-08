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

public sealed class KeyboardSettingsService : IKeyboardSettingsService
{
    public const string KeyboardModuleName = "Keyboard";
    public const string InstantApplyFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const int MaxFunctionParameterLength = 512;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly KeyboardOptions _options;

    public KeyboardSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IOptions<KeyboardOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _options = options.Value;
    }

    public async Task<KeyboardExecuteNowResult> ExecuteNowAsync(
        KeyboardExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return KeyboardExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueKeyboardWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                InstantApplyFunctionName,
                "instant",
                "Keyboard instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? KeyboardExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return KeyboardExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<KeyboardQueueResult> QueueAsync(
        KeyboardQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return KeyboardQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueKeyboardWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                QueuedFunctionName,
                "queued",
                "Keyboard scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? KeyboardQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return KeyboardQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<KeyboardCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return KeyboardCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return KeyboardCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var keyboard = device.KeyboardSettings;
            var hasSettings = keyboard is not null;
            var response = new KeyboardCurrentResponse
            {
                Success = true,
                Message = "Keyboard settings fetched successfully.",
                Data = new KeyboardCurrentData
                {
                    Target = new KeyboardTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new KeyboardCurrentSettingsDto
                    {
                        Delay = keyboard?.Delay ?? 0,
                        RepeatRate = keyboard?.RepeatRate ?? 0,
                        KeyboardLocale = keyboard?.KeyboardLocale ?? string.Empty,
                        ReplaceExistingKeyboard = keyboard?.ReplaceExistingKeyboard ?? false,
                        SettingsVersion = keyboard?.SettingsVersion ?? 0,
                        PendingApply = keyboard?.PendingApply ?? false,
                        LastAppliedVersion = keyboard?.LastAppliedVersion,
                        LastAppliedUtc = keyboard?.LastAppliedUtc,
                        LastApplyStatus = keyboard?.LastApplyStatus,
                        LastApplyMessage = keyboard?.LastApplyMessage
                    },
                    Compat = new KeyboardCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            };

            return KeyboardCurrentResult.Success(response);
        }
        catch (Exception ex)
        {
            return KeyboardCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<KeyboardHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        KeyboardHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return KeyboardHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return KeyboardHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var keyboardModule = KeyboardModuleName;

            var tasksQuery = _dbContext.DeviceTasks
                .AsNoTracking()
                .Where(t => t.DeviceId == device.Id &&
                            t.ModuleName == keyboardModule);

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.Keyboard);

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

            var taskItems = tasks.Select(t => new KeyboardHistoryItem
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

            var logItems = logs.Select(l => new KeyboardHistoryItem
            {
                TaskId = l.TaskId,
                LegacyTaskId = l.LegacyTaskId,
                ModuleName = keyboardModule,
                SettingsVersion = l.SettingsVersion,
                ApplyStatus = l.Status.ToString(),
                ApplyMode = l.ApplyMode,
                Message = l.Message,
                CreatedUtc = l.CreatedUtc
            });

            // Task + log rows may overlap (same apply event); no dedupe in PR4 — admins see both task and log timeline entries.
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

            return KeyboardHistoryResult.Success(new KeyboardHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new KeyboardHistoryData
                {
                    Target = new KeyboardTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new KeyboardPagination
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
            return KeyboardHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    /// <summary>
    /// FusionX <c>WinCELinux.XPKeyboard</c> JSON shape for agent consumption.
    /// LX/CE use the same struct in FusionX DAC instant-apply paths; dedicated LX/CE wrappers are deferred to PR5.
    /// </summary>
    public static string BuildLegacyKeyboardPayload(
        KeyboardTargetRequest target,
        KeyboardSettingsRequest settings)
    {
        var keyboard = new
        {
            iDelay = settings.Delay,
            iRepeat_Rate = settings.RepeatRate,
            Locale = settings.KeyboardLocale,
            IsReplaceExistingKeyboard = settings.ReplaceExistingKeyboard
        };

        object wrapper = target.OsType.Trim().ToUpperInvariant() switch
        {
            "XP" => new { WinCELinux = new { XPKeyboard = keyboard } },
            "LX" => new { WinCELinux = new { XPKeyboard = keyboard } },
            "CE" => new { WinCELinux = new { XPKeyboard = keyboard } },
            _ => new { WinCELinux = new { XPKeyboard = keyboard } }
        };

        return JsonSerializer.Serialize(wrapper);
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.KeyboardSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetKeyboardBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return "EnrollmentStateBlocked";
        }

        var hasPendingKeyboardTask = await _dbContext.DeviceTasks
            .AnyAsync(
                t => t.DeviceId == deviceId &&
                     t.ModuleName == KeyboardModuleName &&
                     (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess),
                cancellationToken);

        return hasPendingKeyboardTask ? "PendingTaskExists" : null;
    }

    private async Task<int> GetNextLegacyTaskIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var maxLegacyId = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId)
            .MaxAsync(t => (int?)t.LegacyTaskId, cancellationToken);

        return (maxLegacyId ?? 0) + 1;
    }

    private async Task<KeyboardWorkResult> QueueKeyboardWorkAsync(
        KeyboardTargetRequest target,
        KeyboardSettingsRequest settings,
        KeyboardExecutionRequest execution,
        KeyboardOptionsRequest options,
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
            if (string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return KeyboardWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return KeyboardWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return KeyboardWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetKeyboardBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return KeyboardWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var keyboard = device.KeyboardSettings;
        if (keyboard is null)
        {
            keyboard = new DeviceKeyboardSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceKeyboardSettings.Add(keyboard);
            device.KeyboardSettings = keyboard;
        }

        keyboard.Delay = settings.Delay;
        keyboard.RepeatRate = settings.RepeatRate;
        keyboard.KeyboardLocale = settings.KeyboardLocale.Trim();
        keyboard.ReplaceExistingKeyboard = settings.ReplaceExistingKeyboard;
        keyboard.SettingsVersion++;
        keyboard.PendingApply = true;
        keyboard.UpdatedBy = adminId;
        keyboard.UpdatedUtc = now;

        var functionPayload = BuildLegacyKeyboardPayload(target, settings);
        if (functionPayload.Length > MaxFunctionParameterLength)
        {
            return KeyboardWorkResult.Failure(
                "ValidationFailed",
                $"Agent payload exceeds {MaxFunctionParameterLength} characters ({functionPayload.Length}). Shorten keyboardLocale or settings.");
        }

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? "SCR"
            : _options.DefaultSignalSuffix.Trim();
        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = KeyboardModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = $"{device.MacAddress.Trim()}&{signalSuffix}",
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.Keyboard,
            keyboard.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return KeyboardWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return KeyboardWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private static string MapTaskApplyMode(string functionName)
    {
        if (string.Equals(functionName, InstantApplyFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return "instant";
        }

        if (string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
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

    private sealed class KeyboardWorkResult
    {
        public KeyboardExecuteNowResult? ExecuteNowResult { get; init; }
        public KeyboardQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static KeyboardWorkResult FromExecuteNow(KeyboardExecuteNowResponse response) =>
            new() { ExecuteNowResult = KeyboardExecuteNowResult.Success(response) };

        public static KeyboardWorkResult FromQueue(KeyboardQueueResponse response) =>
            new() { QueueResult = KeyboardQueueResult.Success(response) };

        public static KeyboardWorkResult Failure(string errorCode, string message) =>
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

    private static KeyboardLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static KeyboardQueueResponse BuildQueueResponse(
        KeyboardTargetRequest target,
        KeyboardExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        return new KeyboardQueueResponse
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new KeyboardQueueData
            {
                TaskId = taskId,
                Target = new KeyboardTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new KeyboardExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };
    }

    private static KeyboardExecuteNowResponse BuildExecuteNowResponse(
        KeyboardTargetRequest target,
        KeyboardExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        return new KeyboardExecuteNowResponse
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new KeyboardExecuteNowData
            {
                TaskId = taskId,
                Target = new KeyboardTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new KeyboardExecutionResponse
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
}
