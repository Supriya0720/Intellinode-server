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

public sealed class DisplaySettingsService : IDisplaySettingsService
{
    public const string DisplayModuleName = "Display";
    public const string InstantApplyFunctionName = "Now";
    public const string QueuedFunctionName = "Update";
    public const int MaxFunctionParameterLength = 2048;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly DisplayOptions _options;

    public DisplaySettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IOptions<DisplayOptions> options)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _options = options.Value;
    }

    public async Task<DisplayExecuteNowResult> ExecuteNowAsync(
        DisplayExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return DisplayExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            var queueResult = await QueueDisplayWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                InstantApplyFunctionName,
                "instant",
                "Display instant apply queued.",
                cancellationToken);

            return queueResult.ExecuteNowResult
                ?? DisplayExecuteNowResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return DisplayExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<DisplayQueueResult> QueueAsync(
        DisplayQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduleType = NormalizeScheduleType(request.Execution.ScheduleType);
            if (!string.Equals(scheduleType, "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return DisplayQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            var queueResult = await QueueDisplayWorkAsync(
                request.Target,
                request.Settings,
                request.Execution,
                request.Options,
                adminId,
                QueuedFunctionName,
                "queued",
                "Display scheduled queue.",
                cancellationToken);

            return queueResult.QueueResult
                ?? DisplayQueueResult.Failure(
                    queueResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    queueResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return DisplayQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<DisplayCurrentResult> GetCurrentAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return DisplayCurrentResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return DisplayCurrentResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var display = device.DisplaySettings;
            var hasSettings = display is not null;
            var response = new DisplayCurrentResponse
            {
                Success = true,
                Message = "Display settings fetched successfully.",
                Data = new DisplayCurrentData
                {
                    Target = new DisplayTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Settings = new DisplayCurrentSettingsDto
                    {
                        Resolution = display?.Resolution ?? string.Empty,
                        ColorDepth = display?.ColorDepth ?? string.Empty,
                        DualDisplayOption = display?.DualDisplayOption ?? string.Empty,
                        SecondaryRotation = display?.SecondaryRotation ?? string.Empty,
                        SettingsVersion = display?.SettingsVersion ?? 0,
                        PendingApply = display?.PendingApply ?? false,
                        LastAppliedVersion = display?.LastAppliedVersion,
                        LastAppliedUtc = display?.LastAppliedUtc,
                        LastApplyStatus = display?.LastApplyStatus,
                        LastApplyMessage = display?.LastApplyMessage
                    },
                    Compat = new DisplayCurrentCompatDto
                    {
                        Source = hasSettings ? "device" : "none"
                    }
                }
            };

            return DisplayCurrentResult.Success(response);
        }
        catch (Exception ex)
        {
            return DisplayCurrentResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<DisplayHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        DisplayHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return DisplayHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return DisplayHistoryResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var statusFilter = query.Status?.Trim();
            var displayModule = DisplayModuleName;

            var tasksQuery = _dbContext.DeviceTasks
                .AsNoTracking()
                .Where(t => t.DeviceId == device.Id &&
                            t.ModuleName == displayModule);

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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.Display);

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

            var taskItems = tasks.Select(t => new DisplayHistoryItem
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

            var logItems = logs.Select(l => new DisplayHistoryItem
            {
                TaskId = l.TaskId,
                LegacyTaskId = l.LegacyTaskId,
                ModuleName = displayModule,
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

            return DisplayHistoryResult.Success(new DisplayHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new DisplayHistoryData
                {
                    Target = new DisplayTargetResponse
                    {
                        MacAddress = device.MacAddress,
                        OsType = ExtractOsType(device.MacAddress)
                    },
                    Items = items,
                    Pagination = new DisplayPagination
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
            return DisplayHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    /// <summary>
    /// FusionX <c>WinCELinux.XPDisplay</c> JSON shape for agent consumption.
    /// Frequency/colour parsing mirrors <c>WindowsDisplayDAC.UpdateToDatabase</c>.
    /// </summary>
    public static string BuildLegacyDisplayPayload(
        DisplayTargetRequest target,
        DisplaySettingsRequest settings)
    {
        var display = new
        {
            strFrequency = ParseFrequencyFromResolution(settings.Resolution),
            strResolution = settings.Resolution,
            strColourDepth = ParseColourDepthFromColorDepth(settings.ColorDepth),
            DualDisplayOption = settings.DualDisplayOption,
            SecondaryDisplayRotation = settings.SecondaryRotation
        };

        object wrapper = target.OsType.Trim().ToUpperInvariant() switch
        {
            "XP" => new { WinCELinux = new { XPDisplay = display } },
            "LX" => new { WinCELinux = new { XPDisplay = display } },
            "CE" => new { WinCELinux = new { XPDisplay = display } },
            _ => new { WinCELinux = new { XPDisplay = display } }
        };

        return JsonSerializer.Serialize(wrapper);
    }

    public static string ParseFrequencyFromResolution(string resolution)
    {
        try
        {
            return resolution.Split(' ')[4];
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string ParseColourDepthFromColorDepth(string colorDepth)
    {
        try
        {
            var parsed = colorDepth.Split('(')[1];
            return parsed.Split('-')[0];
        }
        catch
        {
            return colorDepth;
        }
    }

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.DisplaySettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetDisplayBlockReasonAsync(
        Guid deviceId,
        EnrollmentState enrollmentState,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(enrollmentState))
        {
            return "EnrollmentStateBlocked";
        }

        var hasPendingDisplayTask = await _dbContext.DeviceTasks
            .AnyAsync(
                t => t.DeviceId == deviceId &&
                     t.ModuleName == DisplayModuleName &&
                     (t.Status == DeviceTaskStatus.Pending || t.Status == DeviceTaskStatus.InProcess),
                cancellationToken);

        return hasPendingDisplayTask ? "PendingTaskExists" : null;
    }

    private async Task<int> GetNextLegacyTaskIdAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var maxLegacyId = await _dbContext.DeviceTasks
            .Where(t => t.DeviceId == deviceId)
            .MaxAsync(t => (int?)t.LegacyTaskId, cancellationToken);

        return (maxLegacyId ?? 0) + 1;
    }

    private async Task<DisplayWorkResult> QueueDisplayWorkAsync(
        DisplayTargetRequest target,
        DisplaySettingsRequest settings,
        DisplayExecutionRequest execution,
        DisplayOptionsRequest options,
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
                return DisplayWorkResult.FromQueue(BuildQueueResponse(
                    target,
                    execution,
                    Guid.Empty,
                    now,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return DisplayWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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
            return DisplayWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetDisplayBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return DisplayWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var display = device.DisplaySettings;
        if (display is null)
        {
            display = new DeviceDisplaySettings
            {
                DeviceId = device.Id,
                SettingsVersion = 0,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceDisplaySettings.Add(display);
            device.DisplaySettings = display;
        }

        display.Resolution = settings.Resolution.Trim();
        display.ColorDepth = settings.ColorDepth.Trim();
        display.DualDisplayOption = settings.DualDisplayOption.Trim();
        display.SecondaryRotation = settings.SecondaryRotation.Trim();
        display.SettingsVersion++;
        display.PendingApply = true;
        display.UpdatedBy = adminId;
        display.UpdatedUtc = now;

        var functionPayload = BuildLegacyDisplayPayload(target, settings);
        if (functionPayload.Length > MaxFunctionParameterLength)
        {
            return DisplayWorkResult.Failure(
                "ValidationFailed",
                $"Agent payload exceeds {MaxFunctionParameterLength} characters ({functionPayload.Length}). Shorten display settings strings.");
        }

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? "SCR"
            : _options.DefaultSignalSuffix.Trim();
        var task = new DeviceTask
        {
            DeviceId = device.Id,
            LegacyTaskId = legacyTaskId,
            ModuleName = DisplayModuleName,
            FunctionName = functionName,
            FunctionParameter = functionPayload,
            ExtraData = $"{device.MacAddress.Trim()}&{signalSuffix}",
            Status = DeviceTaskStatus.Pending,
            CreatedUtc = now
        };
        _dbContext.DeviceTasks.Add(task);

        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.Display,
            display.SettingsVersion,
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
            return DisplayWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return DisplayWorkResult.FromExecuteNow(BuildExecuteNowResponse(
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

    private sealed class DisplayWorkResult
    {
        public DisplayExecuteNowResult? ExecuteNowResult { get; init; }
        public DisplayQueueResult? QueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static DisplayWorkResult FromExecuteNow(DisplayExecuteNowResponse response) =>
            new() { ExecuteNowResult = DisplayExecuteNowResult.Success(response) };

        public static DisplayWorkResult FromQueue(DisplayQueueResponse response) =>
            new() { QueueResult = DisplayQueueResult.Success(response) };

        public static DisplayWorkResult Failure(string errorCode, string message) =>
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

    private static DisplayLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static DisplayQueueResponse BuildQueueResponse(
        DisplayTargetRequest target,
        DisplayExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        return new DisplayQueueResponse
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new DisplayQueueData
            {
                TaskId = taskId,
                Target = new DisplayTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new DisplayExecutionResponse
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

    private static DisplayExecuteNowResponse BuildExecuteNowResponse(
        DisplayTargetRequest target,
        DisplayExecutionRequest execution,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId)
    {
        return new DisplayExecuteNowResponse
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new DisplayExecuteNowData
            {
                TaskId = taskId,
                Target = new DisplayTargetResponse
                {
                    MacAddress = target.MacAddress.Trim(),
                    OsType = target.OsType.Trim().ToUpperInvariant()
                },
                Execution = new DisplayExecutionResponse
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
