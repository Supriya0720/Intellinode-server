using System.Text.Json;
using System.Text.Json.Nodes;
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

public sealed class Windows8021xSettingsService : IWindows8021xSettingsService
{
    public const string ModuleName = Windows8021xModuleConstants.ModuleName;
    public const string InstantApplyFunctionName = Windows8021xModuleConstants.InstantFunctionName;
    public const string QueuedFunctionName = Windows8021xModuleConstants.QueuedFunctionName;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly Windows8021xOptions _options;
    private readonly IWindows8021xPayloadBuilder _payloadBuilder;

    public Windows8021xSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IOptions<Windows8021xOptions> options,
        IWindows8021xPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _options = options.Value;
        _payloadBuilder = payloadBuilder;
    }

    public async Task<Windows8021xExecuteNowResult> ExecuteNowAsync(
        Windows8021xExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return Windows8021xExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueWindows8021xWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                InstantApplyFunctionName,
                "instant",
                "Windows 802.1X instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? Windows8021xExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return Windows8021xExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<Windows8021xQueueResult> QueueAsync(
        Windows8021xQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return Windows8021xQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueWindows8021xWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                QueuedFunctionName,
                "queued",
                "Windows 802.1X scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? Windows8021xQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return Windows8021xQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<Windows8021xCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return Windows8021xCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return Windows8021xCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var settings = device.Windows8021xSettings;
            var hasSettings = settings is not null;
            var settingsJson = hasSettings
                ? RedactPasswordFromSettingsJson(settings!.SettingsJson)
                : "{}";

            return Windows8021xCurrentResult.Success(new Windows8021xCurrentResponse
            {
                Success = true,
                Message = "Windows 802.1X settings fetched successfully.",
                Data = new Windows8021xCurrentData
                {
                    Target = new Windows8021xTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new Windows8021xCurrentSettingsDto
                    {
                        SettingsJson = settingsJson,
                        SettingsVersion = settings?.SettingsVersion ?? 0,
                        PendingApply = settings?.PendingApply ?? false,
                        LastAppliedVersion = settings?.LastAppliedVersion,
                        LastAppliedUtc = settings?.LastAppliedUtc,
                        LastApplyStatus = settings?.LastApplyStatus,
                        LastApplyMessage = settings?.LastApplyMessage
                    },
                    Compat = new Windows8021xCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return Windows8021xCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<Windows8021xHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        Windows8021xHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return Windows8021xHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return Windows8021xHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var moduleName = ModuleName;

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.Windows8021x);

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

            var taskItems = tasks.Select(t => new Windows8021xHistoryItem
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

            var logItems = logs.Select(l => new Windows8021xHistoryItem
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

            return Windows8021xHistoryResult.Success(new Windows8021xHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new Windows8021xHistoryData
                {
                    Target = new Windows8021xTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new Windows8021xPagination
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
            return Windows8021xHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static string NormalizeSettingsJson(string settingsJson)
    {
        if (!IsValidSettingsJsonObject(settingsJson))
        {
            throw new ArgumentException("Settings JSON must be a valid JSON object.", nameof(settingsJson));
        }

        var node = JsonNode.Parse(settingsJson)!;
        return node.ToJsonString();
    }

    internal static string RedactPasswordFromSettingsJson(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson) || !IsValidSettingsJsonObject(settingsJson))
        {
            return "{}";
        }

        var node = JsonNode.Parse(settingsJson) as JsonObject;
        if (node is null)
        {
            return "{}";
        }

        if (node.ContainsKey(Windows8021xSensitiveFields.PasswordPropertyName))
        {
            node[Windows8021xSensitiveFields.PasswordPropertyName] = Windows8021xSensitiveFields.RedactedPasswordValue;
        }

        return node.ToJsonString();
    }

    internal static bool IsValidSettingsJsonObject(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.Windows8021xSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetWindows8021xBlockReasonAsync(
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
                     t.ModuleName == ModuleName &&
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

    private async Task<Windows8021xWorkResult> QueueWindows8021xWorkAsync(
        Windows8021xTargetRequest target,
        Windows8021xSettingsRequest settings,
        Windows8021xExecutionRequest execution,
        Windows8021xOptionsRequest options,
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
                return Windows8021xWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return Windows8021xWorkResult.FromExecuteNow(BuildExecuteNowResponse(
                target,
                execution,
                Guid.Empty,
                now,
                options.ReturnLegacySummary,
                correlationId));
        }

        if (!IsValidSettingsJsonObject(settings.SettingsJson))
        {
            return Windows8021xWorkResult.Failure(
                "ValidationFailed",
                "settings.settingsJson must be a valid JSON object.");
        }

        var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
        if (device is null)
        {
            return Windows8021xWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetWindows8021xBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return Windows8021xWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var windows8021x = device.Windows8021xSettings;
        if (windows8021x is null)
        {
            windows8021x = new DeviceWindows8021xSettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindows8021xSettings.Add(windows8021x);
            device.Windows8021xSettings = windows8021x;
        }

        windows8021x.SettingsJson = NormalizeSettingsJson(settings.SettingsJson);
        windows8021x.SettingsVersion++;
        windows8021x.PendingApply = true;
        windows8021x.UpdatedBy = adminId;
        windows8021x.UpdatedUtc = now;

        var functionPayload = _payloadBuilder.BuildCompactTaskReference(windows8021x.SettingsVersion);

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? "Win802_1x"
            : _options.DefaultSignalSuffix.Trim();
        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = ModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = $"{device.MacAddress.Trim()}&{signalSuffix}",
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.Windows8021x,
            windows8021x.SettingsVersion,
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
            return Windows8021xWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return Windows8021xWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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

    private sealed class Windows8021xWorkResult
    {
        public Windows8021xExecuteNowResult? ExecuteNowResult { get; init; }
        public Windows8021xQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static Windows8021xWorkResult FromExecuteNow(Windows8021xExecuteNowResponse response) =>
            new() { ExecuteNowResult = Windows8021xExecuteNowResult.Success(response) };

        public static Windows8021xWorkResult FromQueue(Windows8021xQueueResponse response) =>
            new() { QueueResult = Windows8021xQueueResult.Success(response) };

        public static Windows8021xWorkResult Failure(string errorCode, string message) =>
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

    private static Windows8021xLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static Windows8021xQueueResponse BuildQueueResponse(
        Windows8021xTargetRequest target,
        Windows8021xExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        return new Windows8021xQueueResponse
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new Windows8021xQueueData
            {
                TaskId = taskId,
                Target = new Windows8021xTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new Windows8021xExecutionResponse
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

    private static Windows8021xExecuteNowResponse BuildExecuteNowResponse(
        Windows8021xTargetRequest target,
        Windows8021xExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        return new Windows8021xExecuteNowResponse
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new Windows8021xExecuteNowData
            {
                TaskId = taskId,
                Target = new Windows8021xTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new Windows8021xExecutionResponse
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
