using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Intellinode.Infrastructure.Services;

public sealed class EffectiveAgentSettingsResolver : IEffectiveAgentSettingsResolver
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly AgentServerOptions _fallbackOptions;

    public EffectiveAgentSettingsResolver(
        IntellinodeDbContext dbContext,
        IOptions<AgentServerOptions> fallbackOptions)
    {
        _dbContext = dbContext;
        _fallbackOptions = fallbackOptions.Value;
    }

    public async Task<EffectiveAgentSettings> ResolveEffectiveGeneralAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadDeviceContextAsync(deviceId, cancellationToken);
        if (context.Device is null)
        {
            return AgentSettingsHelper.BuildGeneralFromTenant(await GetTenantDefaultsAsync(TenantDefaults.DefaultTenantId, cancellationToken));
        }

        return ResolveGeneral(context);
    }

    public async Task<AgentAdvancedConfigDto> ResolveEffectiveAdvancedAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadDeviceContextAsync(deviceId, cancellationToken);
        if (context.Device is null)
        {
            var tenant = await GetTenantDefaultsAsync(TenantDefaults.DefaultTenantId, cancellationToken);
            return AgentSettingsHelper.CreateDefaultAdvanced(tenant.DefaultPollIntervalSeconds);
        }

        return ResolveAdvanced(context);
    }

    public async Task<EffectiveDeviceSettingsDto?> ResolveEffectiveCombinedByMacAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceByMacAsync(macAddress, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var context = await LoadDeviceContextAsync(device.Id, cancellationToken);
        var general = ResolveGeneral(context);
        var advanced = ResolveAdvanced(context);

        return new EffectiveDeviceSettingsDto
        {
            MacAddress = device.MacAddress,
            GroupId = device.GroupId,
            GeneralInheritFromGroup = context.Device!.RemoteSettings?.InheritFromGroup ?? true,
            AdvancedInheritFromGroup = context.Device.AgentAdvancedSettings?.InheritFromGroup ?? true,
            GeneralSource = general.Source,
            AdvancedSource = ResolveAdvancedSourceLabel(context),
            General = general,
            Advanced = advanced
        };
    }

    private static string ResolveAdvancedSourceLabel(EffectiveAgentSettingsResolver.DeviceContext context)
    {
        if (context.Device!.AgentAdvancedSettings is { InheritFromGroup: false })
        {
            return "device";
        }

        if (context.GroupAdvanced is not null)
        {
            return "group";
        }

        if (context.Device.AgentAdvancedSettings is not null)
        {
            return "device";
        }

        return "default";
    }

    public async Task<AgentConfigAckResponse> AcknowledgeConfigAsync(
        Guid deviceId,
        AgentConfigAckRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = await LoadDeviceContextAsync(deviceId, cancellationToken);
        if (context.Device is null)
        {
            return new AgentConfigAckResponse { Success = false, Message = "Device not found." };
        }

        var now = DateTime.UtcNow;
        var general = ResolveGeneral(context);
        var advanced = ResolveAdvanced(context);
        var messages = new List<string>();

        if (request.GeneralApplied)
        {
            if (request.SettingsVersion != general.SettingsVersion)
            {
                messages.Add("General settings version mismatch.");
            }
            else
            {
                await ApplyGeneralAckAsync(context, request.SettingsVersion, now, cancellationToken);
            }
        }

        if (request.AdvancedApplied)
        {
            if (request.AdvancedSettingsVersion != advanced.AdvancedSettingsVersion)
            {
                messages.Add("Advanced settings version mismatch.");
            }
            else
            {
                await ApplyAdvancedAckAsync(context, request.AdvancedSettingsVersion, now, cancellationToken);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (messages.Count > 0)
        {
            var refreshed = await BuildAgentConfigAsync(context.Device, cancellationToken);
            return new AgentConfigAckResponse
            {
                Success = false,
                Message = string.Join(' ', messages),
                Config = refreshed
            };
        }

        var config = await BuildAgentConfigAsync(context.Device, cancellationToken);
        return new AgentConfigAckResponse { Success = true, Config = config };
    }

    public async Task<bool> HasPendingConfigAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var general = await ResolveEffectiveGeneralAsync(deviceId, cancellationToken);
        var advanced = await ResolveEffectiveAdvancedAsync(deviceId, cancellationToken);
        return general.PendingApply || advanced.AdvancedPendingApply;
    }

    internal async Task<AgentConfigResponse?> BuildAgentConfigAsync(
        Device device,
        CancellationToken cancellationToken)
    {
        var context = await LoadDeviceContextAsync(device.Id, cancellationToken);
        var general = ResolveGeneral(context);
        var advanced = ResolveAdvanced(context);

        return new AgentConfigResponse
        {
            ServerBaseUrl = general.ServerBaseUrl,
            ApiBaseUrl = general.ApiBaseUrl,
            PollIntervalSeconds = general.PollIntervalSeconds,
            CommunicationType = general.CommunicationType,
            AgentEnabled = general.AgentEnabled,
            SettingsVersion = general.SettingsVersion,
            PendingApply = general.PendingApply,
            Advanced = advanced
        };
    }

    internal async Task<DeviceContext> LoadDeviceContextAsync(Guid deviceId, CancellationToken cancellationToken)
    {
        var device = await _dbContext.Devices
            .Include(d => d.RemoteSettings)
            .Include(d => d.AgentAdvancedSettings)
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken);

        GroupRemoteSettings? groupRemote = null;
        GroupAgentAdvancedSettings? groupAdvanced = null;

        if (device?.GroupId is Guid groupId)
        {
            groupRemote = await _dbContext.GroupRemoteSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken);
            groupAdvanced = await _dbContext.GroupAgentAdvancedSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken);
        }

        var tenantDefaults = await GetTenantDefaultsAsync(device?.TenantId ?? TenantDefaults.DefaultTenantId, cancellationToken);

        return new DeviceContext(device, groupRemote, groupAdvanced, tenantDefaults);
    }

    internal EffectiveAgentSettings ResolveGeneral(DeviceContext context)
    {
        var device = context.Device!;
        var lastApplied = device.RemoteSettings?.LastAppliedVersion;

        if (device.RemoteSettings is not null && !device.RemoteSettings.InheritFromGroup)
        {
            return AgentSettingsHelper.BuildGeneralFromDeviceRow(device.RemoteSettings, context.TenantDefaults, lastApplied);
        }

        if (context.GroupRemote is not null)
        {
            return AgentSettingsHelper.BuildGeneralFromGroupRow(context.GroupRemote, context.TenantDefaults, lastApplied);
        }

        if (device.RemoteSettings is not null)
        {
            return AgentSettingsHelper.BuildGeneralFromDeviceRow(device.RemoteSettings, context.TenantDefaults, lastApplied);
        }

        return AgentSettingsHelper.BuildGeneralFromTenant(context.TenantDefaults);
    }

    internal AgentAdvancedConfigDto ResolveAdvanced(DeviceContext context)
    {
        var device = context.Device!;
        var lastApplied = device.AgentAdvancedSettings?.LastAppliedVersion;

        if (device.AgentAdvancedSettings is not null && !device.AgentAdvancedSettings.InheritFromGroup)
        {
            return AgentSettingsHelper.MapAdvancedFromDevice(device.AgentAdvancedSettings, lastApplied);
        }

        if (context.GroupAdvanced is not null)
        {
            return AgentSettingsHelper.MapAdvancedFromGroup(context.GroupAdvanced, lastApplied);
        }

        if (device.AgentAdvancedSettings is not null)
        {
            return AgentSettingsHelper.MapAdvancedFromDevice(device.AgentAdvancedSettings, lastApplied);
        }

        return AgentSettingsHelper.CreateDefaultAdvanced(context.TenantDefaults.DefaultPollIntervalSeconds);
    }

    internal async Task<TenantAgentDefaults> GetTenantDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var defaults = await _dbContext.TenantAgentDefaults
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, cancellationToken);

        if (defaults is not null)
        {
            return defaults;
        }

        var serverBaseUrl = AgentSettingsHelper.NormalizeBaseUrl(_fallbackOptions.ServerBaseUrl);
        var apiBaseUrl = string.IsNullOrWhiteSpace(_fallbackOptions.ApiBaseUrl)
            ? $"{serverBaseUrl}/api/v1"
            : AgentSettingsHelper.NormalizeBaseUrl(_fallbackOptions.ApiBaseUrl);

        return new TenantAgentDefaults
        {
            TenantId = tenantId,
            ServerBaseUrl = serverBaseUrl,
            ApiBaseUrl = apiBaseUrl,
            DefaultPollIntervalSeconds = _fallbackOptions.DefaultPollIntervalSeconds,
            DefaultCommunicationType = CommunicationType.HTTPS,
            MinPollIntervalHttp = 30,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    internal async Task WriteApplyLogAsync(
        Guid deviceId,
        SettingsKind kind,
        long version,
        string applyMode,
        SettingsApplyStatus status,
        Guid? adminId,
        string? message,
        CancellationToken cancellationToken)
    {
        _dbContext.DeviceSettingsApplyLogs.Add(new DeviceSettingsApplyLog
        {
            DeviceId = deviceId,
            SettingsKind = kind,
            SettingsVersion = version,
            ApplyMode = applyMode,
            Status = status,
            InitiatedBy = adminId,
            Message = message,
            CreatedUtc = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    private async Task ApplyGeneralAckAsync(DeviceContext context, long version, DateTime now, CancellationToken cancellationToken)
    {
        var device = context.Device!;
        var settings = device.RemoteSettings;
        if (settings is null)
        {
            settings = new DeviceRemoteSettings
            {
                DeviceId = device.Id,
                InheritFromGroup = true,
                SettingsVersion = 0,
                CreatedUtc = now
            };
            _dbContext.DeviceRemoteSettings.Add(settings);
            device.RemoteSettings = settings;
        }

        settings.LastAppliedVersion = version;
        settings.LastAppliedUtc = now;

        if (settings.InheritFromGroup == false)
        {
            settings.PendingApply = false;
        }

        await WriteApplyLogAsync(device.Id, SettingsKind.General, version, "instant", SettingsApplyStatus.Applied, null, null, cancellationToken);
    }

    private async Task ApplyAdvancedAckAsync(DeviceContext context, long version, DateTime now, CancellationToken cancellationToken)
    {
        var device = context.Device!;
        var settings = device.AgentAdvancedSettings;
        if (settings is null)
        {
            settings = AgentSettingsHelper.CreateDefaultDeviceAdvanced(device.Id, context.TenantDefaults.DefaultPollIntervalSeconds);
            _dbContext.DeviceAgentAdvancedSettings.Add(settings);
            device.AgentAdvancedSettings = settings;
        }

        settings.LastAppliedVersion = version;
        settings.LastAppliedUtc = now;

        if (settings.InheritFromGroup == false)
        {
            settings.PendingApply = false;
        }

        await WriteApplyLogAsync(device.Id, SettingsKind.Advanced, version, "instant", SettingsApplyStatus.Applied, null, null, cancellationToken);
    }

    private async Task<Device?> FindDeviceByMacAsync(string macAddress, CancellationToken cancellationToken)
    {
        var normalizedMac = macAddress.Trim();
        return await _dbContext.Devices.FirstOrDefaultAsync(
            d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
            cancellationToken);
    }

    internal sealed class DeviceContext(
        Device? device,
        GroupRemoteSettings? groupRemote,
        GroupAgentAdvancedSettings? groupAdvanced,
        TenantAgentDefaults tenantDefaults)
    {
        public Device? Device { get; } = device;
        public GroupRemoteSettings? GroupRemote { get; } = groupRemote;
        public GroupAgentAdvancedSettings? GroupAdvanced { get; } = groupAdvanced;
        public TenantAgentDefaults TenantDefaults { get; } = tenantDefaults;
    }
}
