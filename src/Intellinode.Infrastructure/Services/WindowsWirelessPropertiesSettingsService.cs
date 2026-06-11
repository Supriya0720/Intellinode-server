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

public sealed class WindowsWirelessPropertiesSettingsService : IWindowsWirelessPropertiesSettingsService
{
    public const string ModuleName = WindowsWirelessPropertiesModuleConstants.ModuleName;
    public const string InstantApplyFunctionName = WindowsWirelessPropertiesModuleConstants.InstantFunctionName;
    public const string QueuedFunctionName = WindowsWirelessPropertiesModuleConstants.QueuedFunctionName;

    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;
    private readonly WindowsWirelessPropertiesOptions _options;
    private readonly IWindowsWirelessPropertiesPayloadBuilder _payloadBuilder;

    public WindowsWirelessPropertiesSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver,
        IOptions<WindowsWirelessPropertiesOptions> options,
        IWindowsWirelessPropertiesPayloadBuilder payloadBuilder)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _options = options.Value;
        _payloadBuilder = payloadBuilder;
    }

    public async Task<WindowsWirelessPropertiesExecuteNowResult> ExecuteNowAsync(
        WindowsWirelessPropertiesExecuteNowRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now.");
            }

            if (request.Operation == WirelessProfileOperation.Delete)
            {
                return WindowsWirelessPropertiesExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Use DeleteExecuteNow for delete operations.");
            }

            var workResult = await QueueWirelessProfileWorkAsync(
                request.Target,
                request.Operation,
                request.Profile,
                request.Execution,
                request.Options,
                adminId,
                InstantApplyFunctionName,
                "instant",
                $"Wireless profile {request.Operation} instant apply queued.",
                cancellationToken);

            return workResult.ExecuteNowResult
                ?? WindowsWirelessPropertiesExecuteNowResult.Failure(
                    workResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    workResult.Message ?? "Execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesQueueResult> QueueAsync(
        WindowsWirelessPropertiesQueueRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on this endpoint.");
            }

            if (request.Operation == WirelessProfileOperation.Delete)
            {
                return WindowsWirelessPropertiesQueueResult.Failure(
                    "ValidationFailed",
                    "Use DeleteQueue for delete operations.");
            }

            var workResult = await QueueWirelessProfileWorkAsync(
                request.Target,
                request.Operation,
                request.Profile,
                request.Execution,
                request.Options,
                adminId,
                QueuedFunctionName,
                "queued",
                $"Wireless profile {request.Operation} scheduled queue.",
                cancellationToken);

            return workResult.QueueResult
                ?? WindowsWirelessPropertiesQueueResult.Failure(
                    workResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    workResult.Message ?? "Queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesDeleteExecuteNowResult> DeleteExecuteNowAsync(
        WindowsWirelessPropertiesDeleteRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesDeleteExecuteNowResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on delete execute-now.");
            }

            var workResult = await QueueWirelessProfileDeleteAsync(
                request.Target,
                request.Ssid,
                request.Execution,
                request.Options,
                adminId,
                InstantApplyFunctionName,
                "instant",
                "Wireless profile delete instant apply queued.",
                cancellationToken);

            return workResult.DeleteExecuteNowResult
                ?? WindowsWirelessPropertiesDeleteExecuteNowResult.Failure(
                    workResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    workResult.Message ?? "Delete execute-now failed.");
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesDeleteExecuteNowResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesDeleteQueueResult> DeleteQueueAsync(
        WindowsWirelessPropertiesDeleteRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "Queue", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesDeleteQueueResult.Failure(
                    "ValidationFailed",
                    "Only Queue is supported on delete queue.");
            }

            var workResult = await QueueWirelessProfileDeleteAsync(
                request.Target,
                request.Ssid,
                request.Execution,
                request.Options,
                adminId,
                QueuedFunctionName,
                "queued",
                "Wireless profile delete scheduled queue.",
                cancellationToken);

            return workResult.DeleteQueueResult
                ?? WindowsWirelessPropertiesDeleteQueueResult.Failure(
                    workResult.ErrorCode ?? "LegacyBehaviorExecutionFailed",
                    workResult.Message ?? "Delete queue failed.");
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesDeleteQueueResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesListResult> ListProfilesAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsWirelessPropertiesListResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsWirelessPropertiesListResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var profiles = await _dbContext.DeviceWindowsWirelessProfileSettings
                .AsNoTracking()
                .Where(p => p.DeviceId == device.Id)
                .OrderBy(p => p.Ssid)
                .ToListAsync(cancellationToken);

            return WindowsWirelessPropertiesListResult.Success(new WindowsWirelessPropertiesListResponse
            {
                Success = true,
                Message = "Wireless profiles fetched successfully.",
                Data = new WindowsWirelessPropertiesListData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Profiles = profiles.Select(MapProfileDto).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesListResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesProfileResult> GetProfileAsync(
        string macAddress,
        string ssid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            var normalizedSsid = ssid.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsWirelessPropertiesProfileResult.Failure("ValidationFailed", "macAddress is required.");
            }

            if (string.IsNullOrWhiteSpace(normalizedSsid))
            {
                return WindowsWirelessPropertiesProfileResult.Failure("ValidationFailed", "ssid is required.");
            }

            var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
            if (device is null)
            {
                return WindowsWirelessPropertiesProfileResult.Failure(
                    "DeviceNotFound",
                    $"No device found with MAC address '{normalizedMac}'.");
            }

            var profile = await _dbContext.DeviceWindowsWirelessProfileSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.DeviceId == device.Id && p.Ssid == normalizedSsid,
                    cancellationToken);

            if (profile is null)
            {
                return WindowsWirelessPropertiesProfileResult.Failure(
                    "ProfileNotFound",
                    $"No wireless profile found for SSID '{normalizedSsid}'.");
            }

            return WindowsWirelessPropertiesProfileResult.Success(new WindowsWirelessPropertiesProfileResponse
            {
                Success = true,
                Message = "Wireless profile fetched successfully.",
                Data = new WindowsWirelessPropertiesProfileData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Profile = MapProfileDto(profile)
                }
            });
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesProfileResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesHistoryResult> GetApplyHistoryAsync(
        string macAddress,
        WindowsWirelessPropertiesHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedMac = macAddress.Trim();
            if (string.IsNullOrWhiteSpace(normalizedMac))
            {
                return WindowsWirelessPropertiesHistoryResult.Failure("ValidationFailed", "macAddress is required.");
            }

            var device = await _dbContext.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                    cancellationToken);
            if (device is null)
            {
                return WindowsWirelessPropertiesHistoryResult.Failure(
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
                .Where(l => l.DeviceId == device.Id && l.SettingsKind == SettingsKind.WindowsWirelessProperties);

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

            var taskItems = tasks.Select(t =>
            {
                long? profileKey = null;
                long? settingsVersion = null;
                if (_payloadBuilder.TryParseCompactTaskReference(
                        t.FunctionParameter,
                        out var parsedSettingsVersion,
                        out var parsedProfileKey))
                {
                    profileKey = parsedProfileKey;
                    settingsVersion = parsedSettingsVersion;
                }

                return new WindowsWirelessPropertiesHistoryItem
                {
                    TaskId = t.Id,
                    LegacyTaskId = t.LegacyTaskId,
                    ModuleName = t.ModuleName,
                    FunctionName = t.FunctionName,
                    TaskStatus = t.Status.ToString(),
                    ApplyStatus = MapTaskToApplyStatus(t.Status),
                    ApplyMode = MapTaskApplyMode(t.FunctionName),
                    ProfileKey = profileKey,
                    SettingsVersion = settingsVersion,
                    CreatedUtc = t.CreatedUtc
                };
            });

            var logItems = logs.Select(l => new WindowsWirelessPropertiesHistoryItem
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

            await EnrichHistoryItemsWithSsidAsync(device.Id, tasks, items, cancellationToken);

            return WindowsWirelessPropertiesHistoryResult.Success(new WindowsWirelessPropertiesHistoryResponse
            {
                Success = true,
                Message = "Apply history fetched successfully.",
                Data = new WindowsWirelessPropertiesHistoryData
                {
                    Target = BuildTargetResponse(device.MacAddress),
                    Items = items,
                    Pagination = new WindowsWirelessPropertiesPagination
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
            return WindowsWirelessPropertiesHistoryResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesBulkResult> ExecuteNowBulkAsync(
        WindowsWirelessPropertiesExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureBlock = GetFeatureBlockReason();
            if (featureBlock is not null)
            {
                return WindowsWirelessPropertiesBulkResult.Failure("FeatureDisabled", featureBlock);
            }

            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now bulk.");
            }

            if (request.Operation == WirelessProfileOperation.Delete)
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "ValidationFailed",
                    "Use delete execute-now bulk for delete operations.");
            }

            var uniqueTargets = request.Targets
                .GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return await ExecuteNowForTargetsInternalAsync(
                uniqueTargets,
                request.Operation,
                request.Profile,
                request.Execution,
                request.Options,
                adminId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesBulkResult> ExecuteNowGroupAsync(
        Guid groupId,
        WindowsWirelessPropertiesExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureBlock = GetFeatureBlockReason();
            if (featureBlock is not null)
            {
                return WindowsWirelessPropertiesBulkResult.Failure("FeatureDisabled", featureBlock);
            }

            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on execute-now group.");
            }

            if (request.Operation == WirelessProfileOperation.Delete)
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "ValidationFailed",
                    "Use delete execute-now group for delete operations.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsWirelessProfiles)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsWirelessPropertiesTargetRequest
                {
                    MacAddress = d.MacAddress,
                    OsType = ExtractOsType(d.MacAddress)
                })
                .ToList();

            return await ExecuteNowForTargetsInternalAsync(
                targets,
                request.Operation,
                request.Profile,
                request.Execution,
                request.Options,
                adminId,
                cancellationToken,
                preloadedDevices: devices);
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesBulkResult> DeleteExecuteNowBulkAsync(
        WindowsWirelessPropertiesDeleteExecuteNowBulkRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureBlock = GetFeatureBlockReason();
            if (featureBlock is not null)
            {
                return WindowsWirelessPropertiesBulkResult.Failure("FeatureDisabled", featureBlock);
            }

            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on delete execute-now bulk.");
            }

            var uniqueTargets = request.Targets
                .GroupBy(t => t.MacAddress.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            return await DeleteExecuteNowForTargetsInternalAsync(
                uniqueTargets,
                request.Ssid,
                request.Execution,
                request.Options,
                adminId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    public async Task<WindowsWirelessPropertiesBulkResult> DeleteExecuteNowGroupAsync(
        Guid groupId,
        WindowsWirelessPropertiesDeleteExecuteNowGroupRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureBlock = GetFeatureBlockReason();
            if (featureBlock is not null)
            {
                return WindowsWirelessPropertiesBulkResult.Failure("FeatureDisabled", featureBlock);
            }

            if (!string.Equals(NormalizeScheduleType(request.Execution.ScheduleType), "InstantApply", StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "ValidationFailed",
                    "Only InstantApply is supported on delete execute-now group.");
            }

            var groupExists = await _dbContext.DeviceGroups
                .AnyAsync(
                    g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId,
                    cancellationToken);
            if (!groupExists)
            {
                return WindowsWirelessPropertiesBulkResult.Failure(
                    "GroupNotFound",
                    $"No device group found with id '{groupId}'.");
            }

            var devices = await _dbContext.Devices
                .Include(d => d.WindowsWirelessProfiles)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId &&
                            d.GroupId == groupId &&
                            d.EnrollmentState == EnrollmentState.Active)
                .ToListAsync(cancellationToken);

            var targets = devices
                .Select(d => new WindowsWirelessPropertiesTargetRequest
                {
                    MacAddress = d.MacAddress,
                    OsType = ExtractOsType(d.MacAddress)
                })
                .ToList();

            return await DeleteExecuteNowForTargetsInternalAsync(
                targets,
                request.Ssid,
                request.Execution,
                request.Options,
                adminId,
                cancellationToken,
                preloadedDevices: devices);
        }
        catch (Exception ex)
        {
            return WindowsWirelessPropertiesBulkResult.Failure("LegacyBehaviorExecutionFailed", ex.Message);
        }
    }

    internal static string RedactSensitiveFieldsFromSettingsJson(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return "{}";
        }

        try
        {
            var node = JsonNode.Parse(settingsJson) as JsonObject;
            if (node is null)
            {
                return "{}";
            }

            if (node.ContainsKey(WindowsWirelessPropertiesSensitiveFields.NetworkKeyPropertyName))
            {
                node[WindowsWirelessPropertiesSensitiveFields.NetworkKeyPropertyName] =
                    WindowsWirelessPropertiesSensitiveFields.RedactedValue;
            }

            if (node.ContainsKey(WindowsWirelessPropertiesSensitiveFields.PreSharedKeyPropertyName))
            {
                node[WindowsWirelessPropertiesSensitiveFields.PreSharedKeyPropertyName] =
                    WindowsWirelessPropertiesSensitiveFields.RedactedValue;
            }

            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return "{}";
        }
    }

    private async Task<WindowsWirelessPropertiesWorkResult> QueueWirelessProfileWorkAsync(
        WindowsWirelessPropertiesTargetRequest target,
        WirelessProfileOperation operation,
        WindowsWirelessPropertiesProfileRequest profile,
        WindowsWirelessPropertiesExecutionRequest execution,
        WindowsWirelessPropertiesOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var featureBlock = GetFeatureBlockReason();
        if (featureBlock is not null)
        {
            return WindowsWirelessPropertiesWorkResult.Failure("FeatureDisabled", featureBlock);
        }

        var normalizedMac = target.MacAddress.Trim();
        var normalizedSsid = profile.Ssid.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(normalizedSsid))
        {
            return WindowsWirelessPropertiesWorkResult.Failure("ValidationFailed", "profile.ssid is required.");
        }

        if (options.DryRun)
        {
            return BuildDryRunWorkResult(target, execution, operation, normalizedSsid, 0, functionName, options.ReturnLegacySummary, correlationId);
        }

        var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
        if (device is null)
        {
            return WindowsWirelessPropertiesWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return WindowsWirelessPropertiesWorkResult.Failure("ApplyBlocked", blockReason);
        }

        DeviceWindowsWirelessProfileSettings profileRow = null!;
        if (operation == WirelessProfileOperation.Add)
        {
            var exists = await _dbContext.DeviceWindowsWirelessProfileSettings
                .AnyAsync(p => p.DeviceId == device.Id && p.Ssid == normalizedSsid, cancellationToken);
            if (exists)
            {
                return WindowsWirelessPropertiesWorkResult.Failure(
                    "ProfileAlreadyExists",
                    $"Wireless profile for SSID '{normalizedSsid}' already exists.");
            }

            profileRow = new DeviceWindowsWirelessProfileSettings
            {
                DeviceId = device.Id,
                Ssid = normalizedSsid,
                SettingsVersion = 1,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsWirelessProfileSettings.Add(profileRow);
        }
        else if (operation == WirelessProfileOperation.Update)
        {
            var existingProfile = await _dbContext.DeviceWindowsWirelessProfileSettings
                .FirstOrDefaultAsync(p => p.DeviceId == device.Id && p.Ssid == normalizedSsid, cancellationToken);

            if (existingProfile is null)
            {
                return WindowsWirelessPropertiesWorkResult.Failure(
                    "ProfileNotFound",
                    $"No wireless profile found for SSID '{normalizedSsid}'.");
            }

            profileRow = existingProfile;
            profileRow.SettingsVersion++;
        }
        else
        {
            return WindowsWirelessPropertiesWorkResult.Failure(
                "ValidationFailed",
                $"Unsupported operation '{operation}'.");
        }

        var profileForPayload = CloneProfileWithSsid(profile, normalizedSsid);
        var innerJson = _payloadBuilder.BuildInnerSettingsJson(profileForPayload, operation);
        profileRow.SettingsJson = innerJson;
        profileRow.PendingApply = true;
        profileRow.UpdatedBy = adminId;
        profileRow.UpdatedUtc = now;

        if (profileRow.ProfileKey == 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return await FinalizeQueuedWorkAsync(
            device,
            profileRow,
            target,
            execution,
            options,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            operation,
            correlationId,
            now,
            cancellationToken);
    }

    private async Task<WindowsWirelessPropertiesWorkResult> QueueWirelessProfileDeleteAsync(
        WindowsWirelessPropertiesTargetRequest target,
        string ssid,
        WindowsWirelessPropertiesExecutionRequest execution,
        WindowsWirelessPropertiesOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        var featureBlock = GetFeatureBlockReason();
        if (featureBlock is not null)
        {
            return WindowsWirelessPropertiesWorkResult.Failure("FeatureDisabled", featureBlock);
        }

        var normalizedMac = target.MacAddress.Trim();
        var normalizedSsid = ssid.Trim();
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(normalizedSsid))
        {
            return WindowsWirelessPropertiesWorkResult.Failure("ValidationFailed", "ssid is required.");
        }

        if (options.DryRun)
        {
            return BuildDryRunWorkResult(
                target,
                execution,
                WirelessProfileOperation.Delete,
                normalizedSsid,
                0,
                functionName,
                options.ReturnLegacySummary,
                correlationId,
                isDelete: true);
        }

        var device = await FindDeviceByMacAsync(normalizedMac, cancellationToken);
        if (device is null)
        {
            return WindowsWirelessPropertiesWorkResult.Failure(
                "DeviceNotFound",
                $"No device found with MAC address '{normalizedMac}'.");
        }

        var blockReason = await GetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return WindowsWirelessPropertiesWorkResult.Failure("ApplyBlocked", blockReason);
        }

        var profileRow = await _dbContext.DeviceWindowsWirelessProfileSettings
            .FirstOrDefaultAsync(p => p.DeviceId == device.Id && p.Ssid == normalizedSsid, cancellationToken);
        if (profileRow is null)
        {
            return WindowsWirelessPropertiesWorkResult.Failure(
                "ProfileNotFound",
                $"No wireless profile found for SSID '{normalizedSsid}'.");
        }

        profileRow.SettingsVersion++;
        var deleteProfile = new WindowsWirelessPropertiesProfileRequest { Ssid = normalizedSsid };
        profileRow.SettingsJson = _payloadBuilder.BuildInnerSettingsJson(deleteProfile, WirelessProfileOperation.Delete);
        profileRow.PendingApply = true;
        profileRow.UpdatedBy = adminId;
        profileRow.UpdatedUtc = now;

        return await FinalizeQueuedWorkAsync(
            device,
            profileRow,
            target,
            execution,
            options,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            WirelessProfileOperation.Delete,
            correlationId,
            now,
            cancellationToken,
            isDelete: true);
    }

    private async Task<WindowsWirelessPropertiesWorkResult> FinalizeQueuedWorkAsync(
        Device device,
        DeviceWindowsWirelessProfileSettings profileRow,
        WindowsWirelessPropertiesTargetRequest target,
        WindowsWirelessPropertiesExecutionRequest execution,
        WindowsWirelessPropertiesOptionsRequest options,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        WirelessProfileOperation operation,
        Guid correlationId,
        DateTime now,
        CancellationToken cancellationToken,
        bool isDelete = false)
    {
        var task = await QueueWirelessWorkEntitiesAsync(
            device,
            profileRow,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            now,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isDelete)
        {
            if (string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesWorkResult.FromDeleteQueue(BuildDeleteQueueResponse(
                    target,
                    execution,
                    profileRow.Ssid,
                    profileRow.ProfileKey,
                    task.Id,
                    task.CreatedUtc,
                    options.ReturnLegacySummary,
                    correlationId));
            }

            return WindowsWirelessPropertiesWorkResult.FromDeleteExecuteNow(BuildDeleteExecuteNowResponse(
                target,
                execution,
                profileRow.Ssid,
                profileRow.ProfileKey,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        if (string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return WindowsWirelessPropertiesWorkResult.FromQueue(BuildQueueResponse(
                target,
                execution,
                profileRow.Ssid,
                profileRow.ProfileKey,
                operation,
                task.Id,
                task.CreatedUtc,
                options.ReturnLegacySummary,
                correlationId));
        }

        return WindowsWirelessPropertiesWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target,
            execution,
            profileRow.Ssid,
            profileRow.ProfileKey,
            operation,
            task.Id,
            task.CreatedUtc,
            options.ReturnLegacySummary,
            correlationId));
    }

    private WindowsWirelessPropertiesWorkResult BuildDryRunWorkResult(
        WindowsWirelessPropertiesTargetRequest target,
        WindowsWirelessPropertiesExecutionRequest execution,
        WirelessProfileOperation operation,
        string ssid,
        long profileKey,
        string functionName,
        bool includeLegacySummary,
        Guid correlationId,
        bool isDelete = false)
    {
        if (isDelete)
        {
            if (string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
            {
                return WindowsWirelessPropertiesWorkResult.FromDeleteQueue(BuildDeleteQueueResponse(
                    target, execution, ssid, profileKey, Guid.Empty, DateTime.UtcNow, includeLegacySummary, correlationId));
            }

            return WindowsWirelessPropertiesWorkResult.FromDeleteExecuteNow(BuildDeleteExecuteNowResponse(
                target, execution, ssid, profileKey, Guid.Empty, DateTime.UtcNow, includeLegacySummary, correlationId));
        }

        if (string.Equals(functionName, QueuedFunctionName, StringComparison.OrdinalIgnoreCase))
        {
            return WindowsWirelessPropertiesWorkResult.FromQueue(BuildQueueResponse(
                target, execution, ssid, profileKey, operation, Guid.Empty, DateTime.UtcNow, includeLegacySummary, correlationId));
        }

        return WindowsWirelessPropertiesWorkResult.FromExecuteNow(BuildExecuteNowResponse(
            target, execution, ssid, profileKey, operation, Guid.Empty, DateTime.UtcNow, includeLegacySummary, correlationId));
    }

    private string? GetFeatureBlockReason()
    {
        if (!_options.Enabled)
        {
            return "Windows Wireless Properties is disabled.";
        }

        if (_options.ReadOnly)
        {
            return "Windows Wireless Properties is read-only.";
        }

        return null;
    }

    private async Task<WindowsWirelessPropertiesBulkResult> ExecuteNowForTargetsInternalAsync(
        List<WindowsWirelessPropertiesTargetRequest> uniqueTargets,
        WirelessProfileOperation operation,
        WindowsWirelessPropertiesProfileRequest profileTemplate,
        WindowsWirelessPropertiesExecutionRequest execution,
        WindowsWirelessPropertiesOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken,
        List<Device>? preloadedDevices = null)
    {
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var batchTaskId = Guid.NewGuid();
        var normalizedSsid = profileTemplate.Ssid.Trim();

        if (options.DryRun)
        {
            var dryRunResults = new List<WindowsWirelessPropertiesTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsWirelessPropertiesTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsWirelessPropertiesTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending",
                    Ssid = normalizedSsid
                });
            }

            return WindowsWirelessPropertiesBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                options.ReturnLegacySummary,
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
                .Include(d => d.WindowsWirelessProfiles)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsWirelessPropertiesTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsWirelessPropertiesTargetResult
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
                results.Add(new WindowsWirelessPropertiesTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var queueAttempt = await TryQueueProfileForDeviceAsync(
                device,
                operation,
                profileTemplate,
                adminId,
                InstantApplyFunctionName,
                "instant",
                "Wireless profile bulk instant apply queued.",
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsWirelessPropertiesTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsWirelessPropertiesTargetResult
            {
                MacAddress = mac,
                Status = "Pending",
                Ssid = queueAttempt.Ssid,
                ProfileKey = queueAttempt.ProfileKey
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsWirelessPropertiesBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<WindowsWirelessPropertiesBulkResult> DeleteExecuteNowForTargetsInternalAsync(
        List<WindowsWirelessPropertiesTargetRequest> uniqueTargets,
        string ssid,
        WindowsWirelessPropertiesExecutionRequest execution,
        WindowsWirelessPropertiesOptionsRequest options,
        Guid? adminId,
        CancellationToken cancellationToken,
        List<Device>? preloadedDevices = null)
    {
        var correlationId = options.CorrelationId ?? Guid.NewGuid();
        var batchTaskId = Guid.NewGuid();
        var normalizedSsid = ssid.Trim();

        if (options.DryRun)
        {
            var dryRunResults = new List<WindowsWirelessPropertiesTargetResult>(uniqueTargets.Count);
            foreach (var target in uniqueTargets)
            {
                var mac = target.MacAddress.Trim();
                if (!IsXpDevice(mac))
                {
                    dryRunResults.Add(new WindowsWirelessPropertiesTargetResult
                    {
                        MacAddress = mac,
                        Status = "Blocked",
                        Reason = "UnsupportedOsType"
                    });
                    continue;
                }

                dryRunResults.Add(new WindowsWirelessPropertiesTargetResult
                {
                    MacAddress = mac,
                    Status = "Pending",
                    Ssid = normalizedSsid
                });
            }

            return WindowsWirelessPropertiesBulkResult.Success(BuildBulkResponse(
                batchTaskId,
                uniqueTargets.Count,
                dryRunResults.Count(r => r.Status == "Pending"),
                dryRunResults.Count(r => r.Status == "Blocked"),
                dryRunResults,
                options.ReturnLegacySummary,
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
                .Include(d => d.WindowsWirelessProfiles)
                .Where(d => d.TenantId == TenantDefaults.DefaultTenantId && macs.Contains(d.MacAddress))
                .ToListAsync(cancellationToken);
            byMac = devices.ToDictionary(d => d.MacAddress, StringComparer.OrdinalIgnoreCase);
        }

        var results = new List<WindowsWirelessPropertiesTargetResult>(uniqueTargets.Count);
        var firstAcceptedTaskId = Guid.Empty;
        var accepted = 0;
        var blocked = 0;

        foreach (var target in uniqueTargets)
        {
            var mac = target.MacAddress.Trim();
            if (!byMac.TryGetValue(mac, out var device))
            {
                blocked++;
                results.Add(new WindowsWirelessPropertiesTargetResult
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
                results.Add(new WindowsWirelessPropertiesTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = "UnsupportedOsType"
                });
                continue;
            }

            var queueAttempt = await TryQueueDeleteForDeviceAsync(
                device,
                normalizedSsid,
                adminId,
                InstantApplyFunctionName,
                "instant",
                "Wireless profile delete bulk instant apply queued.",
                cancellationToken);

            if (!queueAttempt.Success)
            {
                blocked++;
                results.Add(new WindowsWirelessPropertiesTargetResult
                {
                    MacAddress = mac,
                    Status = "Blocked",
                    Reason = queueAttempt.Reason
                });
                continue;
            }

            firstAcceptedTaskId = firstAcceptedTaskId == Guid.Empty ? queueAttempt.Task!.Id : firstAcceptedTaskId;
            accepted++;
            results.Add(new WindowsWirelessPropertiesTargetResult
            {
                MacAddress = mac,
                Status = "Pending",
                Ssid = queueAttempt.Ssid,
                ProfileKey = queueAttempt.ProfileKey
            });
        }

        if (accepted > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return WindowsWirelessPropertiesBulkResult.Success(BuildBulkResponse(
            firstAcceptedTaskId == Guid.Empty ? batchTaskId : firstAcceptedTaskId,
            uniqueTargets.Count,
            accepted,
            blocked,
            results,
            options.ReturnLegacySummary,
            correlationId));
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason, long? ProfileKey, string? Ssid)> TryQueueProfileForDeviceAsync(
        Device device,
        WirelessProfileOperation operation,
        WindowsWirelessPropertiesProfileRequest profileTemplate,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            return (false, null, "EnrollmentStateBlocked", null, null);
        }

        var blockReason = await GetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason, null, null);
        }

        var normalizedSsid = profileTemplate.Ssid.Trim();
        var now = DateTime.UtcNow;
        DeviceWindowsWirelessProfileSettings profileRow = null!;

        if (operation == WirelessProfileOperation.Add)
        {
            var exists = device.WindowsWirelessProfiles.Any(p => p.Ssid == normalizedSsid) ||
                         await _dbContext.DeviceWindowsWirelessProfileSettings
                             .AnyAsync(p => p.DeviceId == device.Id && p.Ssid == normalizedSsid, cancellationToken);
            if (exists)
            {
                return (false, null, "ProfileAlreadyExists", null, null);
            }

            profileRow = new DeviceWindowsWirelessProfileSettings
            {
                DeviceId = device.Id,
                Ssid = normalizedSsid,
                SettingsVersion = 1,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.DeviceWindowsWirelessProfileSettings.Add(profileRow);
            device.WindowsWirelessProfiles.Add(profileRow);
        }
        else if (operation == WirelessProfileOperation.Update)
        {
            var existingProfile = device.WindowsWirelessProfiles
                .FirstOrDefault(p => p.Ssid == normalizedSsid)
                ?? await _dbContext.DeviceWindowsWirelessProfileSettings
                    .FirstOrDefaultAsync(p => p.DeviceId == device.Id && p.Ssid == normalizedSsid, cancellationToken);

            if (existingProfile is null)
            {
                return (false, null, "ProfileNotFound", null, null);
            }

            profileRow = existingProfile;
            profileRow.SettingsVersion++;
        }
        else
        {
            return (false, null, "ValidationFailed", null, null);
        }

        var profileForPayload = CloneProfileWithSsid(profileTemplate, normalizedSsid);
        profileRow.SettingsJson = _payloadBuilder.BuildInnerSettingsJson(profileForPayload, operation);
        profileRow.PendingApply = true;
        profileRow.UpdatedBy = adminId;
        profileRow.UpdatedUtc = now;

        if (profileRow.ProfileKey == 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var task = await QueueWirelessWorkEntitiesAsync(
            device,
            profileRow,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            now,
            cancellationToken);

        return (true, task, null, profileRow.ProfileKey, profileRow.Ssid);
    }

    private async Task<(bool Success, DeviceTask? Task, string? Reason, long? ProfileKey, string? Ssid)> TryQueueDeleteForDeviceAsync(
        Device device,
        string ssid,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        CancellationToken cancellationToken)
    {
        if (!DeviceEnrollmentGuard.IsManaged(device.EnrollmentState))
        {
            return (false, null, "EnrollmentStateBlocked", null, null);
        }

        var blockReason = await GetBlockReasonAsync(device.Id, device.EnrollmentState, cancellationToken);
        if (blockReason is not null)
        {
            return (false, null, blockReason, null, null);
        }

        var normalizedSsid = ssid.Trim();
        var profileRow = device.WindowsWirelessProfiles
            .FirstOrDefault(p => p.Ssid == normalizedSsid)
            ?? await _dbContext.DeviceWindowsWirelessProfileSettings
                .FirstOrDefaultAsync(p => p.DeviceId == device.Id && p.Ssid == normalizedSsid, cancellationToken);

        if (profileRow is null)
        {
            return (false, null, "ProfileNotFound", null, null);
        }

        var now = DateTime.UtcNow;
        profileRow.SettingsVersion++;
        var deleteProfile = new WindowsWirelessPropertiesProfileRequest { Ssid = normalizedSsid };
        profileRow.SettingsJson = _payloadBuilder.BuildInnerSettingsJson(deleteProfile, WirelessProfileOperation.Delete);
        profileRow.PendingApply = true;
        profileRow.UpdatedBy = adminId;
        profileRow.UpdatedUtc = now;

        var task = await QueueWirelessWorkEntitiesAsync(
            device,
            profileRow,
            adminId,
            functionName,
            applyMode,
            applyLogMessage,
            now,
            cancellationToken);

        return (true, task, null, profileRow.ProfileKey, profileRow.Ssid);
    }

    private async Task<DeviceTask> QueueWirelessWorkEntitiesAsync(
        Device device,
        DeviceWindowsWirelessProfileSettings profileRow,
        Guid? adminId,
        string functionName,
        string applyMode,
        string applyLogMessage,
        DateTime now,
        CancellationToken cancellationToken)
    {
        _dbContext.DeviceWindowsWirelessProfileSettingsSnapshots.Add(new DeviceWindowsWirelessProfileSettingsSnapshot
        {
            DeviceId = device.Id,
            ProfileKey = profileRow.ProfileKey,
            SettingsVersion = profileRow.SettingsVersion,
            SettingsJson = profileRow.SettingsJson,
            CreatedUtc = now
        });

        var functionPayload = _payloadBuilder.BuildCompactTaskReference(
            profileRow.SettingsVersion,
            profileRow.ProfileKey);

        var legacyTaskId = await GetNextLegacyTaskIdAsync(device.Id, cancellationToken);
        var signalSuffix = string.IsNullOrWhiteSpace(_options.DefaultSignalSuffix)
            ? WindowsWirelessPropertiesModuleConstants.DefaultSignalSuffix
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
            SettingsKind.WindowsWirelessProperties,
            profileRow.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            applyLogMessage,
            cancellationToken,
            task.Id,
            legacyTaskId);

        return task;
    }

    private static WindowsWirelessPropertiesBulkResponse BuildBulkResponse(
        Guid taskId,
        int totalTargets,
        int accepted,
        int blocked,
        List<WindowsWirelessPropertiesTargetResult> results,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Bulk execute-now accepted.",
            Data = new WindowsWirelessPropertiesBulkData
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

    private static bool IsXpDevice(string macAddress) =>
        string.Equals(ExtractOsSuffix(macAddress), "XP", StringComparison.OrdinalIgnoreCase);

    private async Task<Device?> FindDeviceByMacAsync(string normalizedMac, CancellationToken cancellationToken) =>
        await _dbContext.Devices
            .Include(d => d.WindowsWirelessProfiles)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);

    private async Task<string?> GetBlockReasonAsync(
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

    private static WindowsWirelessPropertiesProfileRequest CloneProfileWithSsid(
        WindowsWirelessPropertiesProfileRequest profile,
        string normalizedSsid)
    {
        return new WindowsWirelessPropertiesProfileRequest
        {
            Ssid = normalizedSsid,
            NetworkAuthentication = profile.NetworkAuthentication,
            DataEncryption = profile.DataEncryption,
            NetworkKey = profile.NetworkKey,
            PreSharedKey = profile.PreSharedKey,
            KeyIndex = profile.KeyIndex,
            NetworkName = profile.NetworkName,
            Status = profile.Status,
            ConnectWhenInRange = profile.ConnectWhenInRange,
            ConnectNonBroadcasting = profile.ConnectNonBroadcasting,
            Text2 = profile.Text2,
            Text3 = profile.Text3
        };
    }

    private static WindowsWirelessPropertiesProfileDto MapProfileDto(DeviceWindowsWirelessProfileSettings profile) =>
        new()
        {
            ProfileKey = profile.ProfileKey,
            Ssid = profile.Ssid,
            SettingsJson = RedactSensitiveFieldsFromSettingsJson(profile.SettingsJson),
            SettingsVersion = profile.SettingsVersion,
            PendingApply = profile.PendingApply,
            LastAppliedVersion = profile.LastAppliedVersion,
            LastAppliedUtc = profile.LastAppliedUtc,
            LastApplyStatus = profile.LastApplyStatus,
            LastApplyMessage = profile.LastApplyMessage
        };

    private static WindowsWirelessPropertiesTargetResponse BuildTargetResponse(string macAddress) =>
        new()
        {
            MacAddress = macAddress.Trim(),
            OsType = ExtractOsType(macAddress)
        };

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

    private static string NormalizeScheduleType(string? scheduleType) =>
        string.IsNullOrWhiteSpace(scheduleType) ? "InstantApply" : scheduleType.Trim();

    private async Task EnrichHistoryItemsWithSsidAsync(
        Guid deviceId,
        IReadOnlyList<DeviceTask> moduleTasks,
        IList<WindowsWirelessPropertiesHistoryItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var profileSsids = await _dbContext.DeviceWindowsWirelessProfileSettings
            .AsNoTracking()
            .Where(p => p.DeviceId == deviceId)
            .ToDictionaryAsync(p => p.ProfileKey, p => p.Ssid, cancellationToken);

        var tasksById = moduleTasks.ToDictionary(t => t.Id);

        foreach (var item in items.Where(i => string.IsNullOrWhiteSpace(i.Ssid)))
        {
            var (profileKey, _) = ResolveHistoryProfileReference(item, tasksById);
            if (!profileKey.HasValue || !profileSsids.TryGetValue(profileKey.Value, out var liveSsid))
            {
                continue;
            }

            item.Ssid = liveSsid;
        }

        var snapshotKeys = items
            .Where(i => string.IsNullOrWhiteSpace(i.Ssid))
            .Select(i => ResolveHistoryProfileReference(i, tasksById))
            .Where(r => r.ProfileKey.HasValue && r.SettingsVersion.HasValue)
            .Select(r => (r.ProfileKey!.Value, r.SettingsVersion!.Value))
            .Distinct()
            .ToList();

        if (snapshotKeys.Count == 0)
        {
            return;
        }

        var snapshots = await _dbContext.DeviceWindowsWirelessProfileSettingsSnapshots
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId)
            .ToListAsync(cancellationToken);

        var snapshotLookup = snapshots.ToDictionary(
            s => (s.ProfileKey, s.SettingsVersion),
            s => s.SettingsJson);

        foreach (var item in items.Where(i => string.IsNullOrWhiteSpace(i.Ssid)))
        {
            var (profileKey, settingsVersion) = ResolveHistoryProfileReference(item, tasksById);
            if (!profileKey.HasValue || !settingsVersion.HasValue ||
                !snapshotLookup.TryGetValue((profileKey.Value, settingsVersion.Value), out var settingsJson))
            {
                continue;
            }

            if (WindowsWirelessPropertiesPayloadShape.TryExtractSsidFromInnerSettingsJson(settingsJson, out var ssid))
            {
                item.Ssid = ssid;
            }
        }
    }

    private (long? ProfileKey, long? SettingsVersion) ResolveHistoryProfileReference(
        WindowsWirelessPropertiesHistoryItem item,
        IReadOnlyDictionary<Guid, DeviceTask> tasksById)
    {
        var profileKey = item.ProfileKey;
        var settingsVersion = item.SettingsVersion;

        if (item.TaskId.HasValue && tasksById.TryGetValue(item.TaskId.Value, out var task) &&
            _payloadBuilder.TryParseCompactTaskReference(
                task.FunctionParameter,
                out var parsedSettingsVersion,
                out var parsedProfileKey))
        {
            profileKey ??= parsedProfileKey;
            settingsVersion ??= parsedSettingsVersion;
        }

        return (profileKey, settingsVersion);
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

    private static WindowsWirelessPropertiesLegacySummary BuildLegacySummary(string qualifiedMsg) =>
        new()
        {
            ErrorMsg = "...$ApplyGreenSuccess",
            QualifiedMsg = qualifiedMsg,
            DtApproved = [],
            HtmlData = string.Empty
        };

    private static WindowsWirelessPropertiesExecuteNowResponse BuildExecuteNowResponse(
        WindowsWirelessPropertiesTargetRequest target,
        WindowsWirelessPropertiesExecutionRequest execution,
        string ssid,
        long profileKey,
        WirelessProfileOperation operation,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Execute Now queued successfully.",
            Data = new WindowsWirelessPropertiesExecuteNowData
            {
                TaskId = taskId,
                ProfileKey = profileKey,
                Ssid = ssid,
                Operation = operation,
                Target = BuildTargetResponse(target.MacAddress),
                Execution = new WindowsWirelessPropertiesExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsWirelessPropertiesQueueResponse BuildQueueResponse(
        WindowsWirelessPropertiesTargetRequest target,
        WindowsWirelessPropertiesExecutionRequest execution,
        string ssid,
        long profileKey,
        WirelessProfileOperation operation,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Queue accepted.",
            Data = new WindowsWirelessPropertiesQueueData
            {
                TaskId = taskId,
                ProfileKey = profileKey,
                Ssid = ssid,
                Operation = operation,
                Target = BuildTargetResponse(target.MacAddress),
                Execution = new WindowsWirelessPropertiesExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsWirelessPropertiesDeleteExecuteNowResponse BuildDeleteExecuteNowResponse(
        WindowsWirelessPropertiesTargetRequest target,
        WindowsWirelessPropertiesExecutionRequest execution,
        string ssid,
        long profileKey,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Delete execute-now queued successfully.",
            Data = new WindowsWirelessPropertiesDeleteExecuteNowData
            {
                TaskId = taskId,
                ProfileKey = profileKey,
                Ssid = ssid,
                Target = BuildTargetResponse(target.MacAddress),
                Execution = new WindowsWirelessPropertiesExecutionResponse
                {
                    ScheduleType = NormalizeScheduleType(execution.ScheduleType),
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private static WindowsWirelessPropertiesDeleteQueueResponse BuildDeleteQueueResponse(
        WindowsWirelessPropertiesTargetRequest target,
        WindowsWirelessPropertiesExecutionRequest execution,
        string ssid,
        long profileKey,
        Guid taskId,
        DateTime queuedAtUtc,
        bool includeLegacySummary,
        Guid correlationId) =>
        new()
        {
            Success = true,
            Message = "Delete queue accepted.",
            Data = new WindowsWirelessPropertiesDeleteQueueData
            {
                TaskId = taskId,
                ProfileKey = profileKey,
                Ssid = ssid,
                Target = BuildTargetResponse(target.MacAddress),
                Execution = new WindowsWirelessPropertiesExecutionResponse
                {
                    ScheduleType = "Queue",
                    Status = "Pending",
                    QueuedAtUtc = queuedAtUtc
                },
                LegacySummary = includeLegacySummary ? BuildLegacySummary("1") : null,
                CorrelationId = correlationId
            }
        };

    private sealed class WindowsWirelessPropertiesWorkResult
    {
        public WindowsWirelessPropertiesExecuteNowResult? ExecuteNowResult { get; init; }
        public WindowsWirelessPropertiesQueueResult? QueueResult { get; init; }
        public WindowsWirelessPropertiesDeleteExecuteNowResult? DeleteExecuteNowResult { get; init; }
        public WindowsWirelessPropertiesDeleteQueueResult? DeleteQueueResult { get; init; }
        public string? ErrorCode { get; init; }
        public string? Message { get; init; }

        public static WindowsWirelessPropertiesWorkResult FromExecuteNow(WindowsWirelessPropertiesExecuteNowResponse response) =>
            new() { ExecuteNowResult = WindowsWirelessPropertiesExecuteNowResult.Success(response) };

        public static WindowsWirelessPropertiesWorkResult FromQueue(WindowsWirelessPropertiesQueueResponse response) =>
            new() { QueueResult = WindowsWirelessPropertiesQueueResult.Success(response) };

        public static WindowsWirelessPropertiesWorkResult FromDeleteExecuteNow(WindowsWirelessPropertiesDeleteExecuteNowResponse response) =>
            new() { DeleteExecuteNowResult = WindowsWirelessPropertiesDeleteExecuteNowResult.Success(response) };

        public static WindowsWirelessPropertiesWorkResult FromDeleteQueue(WindowsWirelessPropertiesDeleteQueueResponse response) =>
            new() { DeleteQueueResult = WindowsWirelessPropertiesDeleteQueueResult.Success(response) };

        public static WindowsWirelessPropertiesWorkResult Failure(string errorCode, string message) =>
            new() { ErrorCode = errorCode, Message = message };
    }
}
