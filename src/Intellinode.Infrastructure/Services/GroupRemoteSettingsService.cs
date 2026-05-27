using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class GroupRemoteSettingsService : IGroupRemoteSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;

    public GroupRemoteSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver)
    {
        _dbContext = dbContext;
        _resolver = resolver;
    }

    public async Task<GroupRemoteSettingsDto?> GetGroupRemoteSettingsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await FindGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        if (group.RemoteSettings is null)
        {
            var tenant = await _resolver.GetTenantDefaultsAsync(group.TenantId, cancellationToken);
            var (host, port) = AgentSettingsHelper.ParseHostPort(tenant.ServerBaseUrl);
            return new GroupRemoteSettingsDto
            {
                GroupId = groupId,
                ServerHost = host,
                ServerPort = port,
                PollIntervalSeconds = tenant.DefaultPollIntervalSeconds,
                CommunicationType = tenant.DefaultCommunicationType,
                AgentEnabled = true,
                SettingsVersion = 0,
                CreatedUtc = tenant.UpdatedUtc,
                UpdatedUtc = tenant.UpdatedUtc
            };
        }

        return MapGroupRemoteToDto(group.RemoteSettings);
    }

    public async Task<GroupRemoteSettingsDto?> UpsertGroupRemoteSettingsAsync(
        Guid groupId,
        UpsertGroupRemoteSettingsRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        var group = await FindGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var settings = group.RemoteSettings;
        if (settings is null)
        {
            settings = new GroupRemoteSettings
            {
                GroupId = groupId,
                CreatedUtc = DateTime.UtcNow
            };
            _dbContext.GroupRemoteSettings.Add(settings);
            group.RemoteSettings = settings;
        }

        settings.ServerHost = request.ServerHost.Trim();
        settings.ServerPort = request.ServerPort;
        settings.PollIntervalSeconds = request.PollIntervalSeconds;
        settings.CommunicationType = request.CommunicationType;
        settings.AgentEnabled = request.AgentEnabled;
        settings.DesiredGroupName = request.DesiredGroupName;
        settings.AgentHostName = request.AgentHostName;
        settings.UseDhcpDiscovery = request.UseDhcpDiscovery;
        settings.ApplyOnReboot = request.ApplyOnReboot;
        settings.SettingsVersion++;
        settings.UpdatedUtc = DateTime.UtcNow;

        await MarkInheritingDevicesPendingAsync(
            groupId,
            SettingsKind.General,
            settings.SettingsVersion,
            request.ApplyOnReboot ? "reboot" : "instant",
            adminId,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapGroupRemoteToDto(settings);
    }

    public async Task<GroupAgentAdvancedSettingsDto?> GetGroupAdvancedSettingsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var group = await FindGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        if (group.AgentAdvancedSettings is null)
        {
            var tenant = await _resolver.GetTenantDefaultsAsync(group.TenantId, cancellationToken);
            var defaults = AgentSettingsHelper.CreateDefaultGroupAdvanced(groupId, tenant.DefaultPollIntervalSeconds);
            return MapGroupAdvancedToDto(defaults);
        }

        return MapGroupAdvancedToDto(group.AgentAdvancedSettings);
    }

    public async Task<GroupAgentAdvancedSettingsDto?> UpsertGroupAdvancedSettingsAsync(
        Guid groupId,
        UpsertGroupAgentAdvancedSettingsRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        var group = await FindGroupAsync(groupId, cancellationToken);
        if (group is null)
        {
            return null;
        }

        var tenant = await _resolver.GetTenantDefaultsAsync(group.TenantId, cancellationToken);
        var settings = group.AgentAdvancedSettings;
        if (settings is null)
        {
            settings = AgentSettingsHelper.CreateDefaultGroupAdvanced(groupId, tenant.DefaultPollIntervalSeconds);
            _dbContext.GroupAgentAdvancedSettings.Add(settings);
            group.AgentAdvancedSettings = settings;
        }

        AgentSettingsHelper.ApplyAdvancedRequest(settings, request);
        settings.SettingsVersion++;
        settings.UpdatedUtc = DateTime.UtcNow;

        await MarkInheritingDevicesPendingAsync(
            groupId,
            SettingsKind.Advanced,
            settings.SettingsVersion,
            request.ApplyOnNextReboot ? "reboot" : "instant",
            adminId,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapGroupAdvancedToDto(settings);
    }

    public async Task<PropagateGroupSettingsResponse?> PropagatePendingApplyAsync(
        Guid groupId,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        var group = await _dbContext.DeviceGroups
            .Include(g => g.RemoteSettings)
            .Include(g => g.AgentAdvancedSettings)
            .Include(g => g.Devices)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId, cancellationToken);

        if (group is null)
        {
            return null;
        }

        var marked = 0;
        foreach (var device in group.Devices)
        {
            var inheritsGeneral = device.RemoteSettings?.InheritFromGroup ?? true;
            var inheritsAdvanced = device.AgentAdvancedSettings?.InheritFromGroup ?? true;

            if (inheritsGeneral && group.RemoteSettings is not null)
            {
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.General,
                    group.RemoteSettings.SettingsVersion,
                    "instant",
                    SettingsApplyStatus.Pending,
                    adminId,
                    "Group settings propagated.",
                    cancellationToken);
                marked++;
            }

            if (inheritsAdvanced && group.AgentAdvancedSettings is not null)
            {
                await _resolver.WriteApplyLogAsync(
                    device.Id,
                    SettingsKind.Advanced,
                    group.AgentAdvancedSettings.SettingsVersion,
                    "instant",
                    SettingsApplyStatus.Pending,
                    adminId,
                    "Group advanced settings propagated.",
                    cancellationToken);
                marked++;
            }
        }

        if (group.RemoteSettings is not null)
        {
            group.RemoteSettings.SettingsVersion++;
            group.RemoteSettings.UpdatedUtc = DateTime.UtcNow;
        }

        if (group.AgentAdvancedSettings is not null)
        {
            group.AgentAdvancedSettings.SettingsVersion++;
            group.AgentAdvancedSettings.UpdatedUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PropagateGroupSettingsResponse
        {
            GroupId = groupId,
            DevicesMarkedPending = marked
        };
    }

    private async Task MarkInheritingDevicesPendingAsync(
        Guid groupId,
        SettingsKind kind,
        long version,
        string applyMode,
        Guid? adminId,
        CancellationToken cancellationToken)
    {
        var devices = await _dbContext.Devices
            .Include(d => d.RemoteSettings)
            .Include(d => d.AgentAdvancedSettings)
            .Where(d => d.GroupId == groupId && d.TenantId == TenantDefaults.DefaultTenantId)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            var inherits = kind switch
            {
                SettingsKind.General => device.RemoteSettings?.InheritFromGroup ?? true,
                SettingsKind.Advanced => device.AgentAdvancedSettings?.InheritFromGroup ?? true,
                _ => false
            };

            if (!inherits)
            {
                continue;
            }

            await _resolver.WriteApplyLogAsync(
                device.Id,
                kind,
                version,
                applyMode,
                SettingsApplyStatus.Pending,
                adminId,
                "Group settings updated.",
                cancellationToken);
        }
    }

    private async Task<DeviceGroup?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken) =>
        await _dbContext.DeviceGroups
            .Include(g => g.RemoteSettings)
            .Include(g => g.AgentAdvancedSettings)
            .FirstOrDefaultAsync(g => g.Id == groupId && g.TenantId == TenantDefaults.DefaultTenantId, cancellationToken);

    private static GroupRemoteSettingsDto MapGroupRemoteToDto(GroupRemoteSettings settings) =>
        new()
        {
            GroupId = settings.GroupId,
            ServerHost = settings.ServerHost,
            ServerPort = settings.ServerPort,
            PollIntervalSeconds = settings.PollIntervalSeconds,
            CommunicationType = settings.CommunicationType,
            AgentEnabled = settings.AgentEnabled,
            DesiredGroupName = settings.DesiredGroupName,
            AgentHostName = settings.AgentHostName,
            UseDhcpDiscovery = settings.UseDhcpDiscovery,
            ApplyOnReboot = settings.ApplyOnReboot,
            SettingsVersion = settings.SettingsVersion,
            CreatedUtc = settings.CreatedUtc,
            UpdatedUtc = settings.UpdatedUtc
        };

    private static GroupAgentAdvancedSettingsDto MapGroupAdvancedToDto(GroupAgentAdvancedSettings settings) =>
        new()
        {
            GroupId = settings.GroupId,
            DebugLevel = settings.DebugLevel,
            HeartbeatIntervalSeconds = settings.HeartbeatIntervalSeconds,
            ApplicationIntervalSeconds = settings.ApplicationIntervalSeconds,
            UsbLogsEnabled = settings.UsbLogsEnabled,
            ApplicationLogsEnabled = settings.ApplicationLogsEnabled,
            BootLogsEnabled = settings.BootLogsEnabled,
            ScreensaverLogsEnabled = settings.ScreensaverLogsEnabled,
            YumMonitorEnabled = settings.YumMonitorEnabled,
            SignalrMonitoringEnabled = settings.SignalrMonitoringEnabled,
            ConnectionType = settings.ConnectionType,
            DhcpPollIntervalSeconds = settings.DhcpPollIntervalSeconds,
            AlwaysApply = settings.AlwaysApply,
            ApplyOnNextReboot = settings.ApplyOnNextReboot,
            SettingsVersion = settings.SettingsVersion,
            CreatedUtc = settings.CreatedUtc,
            UpdatedUtc = settings.UpdatedUtc
        };
}
