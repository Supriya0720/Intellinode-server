using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class DeviceRemoteSettingsService : IDeviceRemoteSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;

    public DeviceRemoteSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver)
    {
        _dbContext = dbContext;
        _resolver = resolver;
    }

    public async Task<DeviceRemoteSettingsDto?> GetByMacAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceByMacAsync(macAddress, cancellationToken);
        if (device is null)
        {
            return null;
        }

        if (device.RemoteSettings is not null)
        {
            return MapToDto(device.MacAddress, device.RemoteSettings);
        }

        var tenantDefaults = await _resolver.GetTenantDefaultsAsync(device.TenantId, cancellationToken);
        return MapToDtoFromTenantDefaults(device.MacAddress, tenantDefaults);
    }

    public async Task<DeviceRemoteSettingsDto?> UpsertByMacAsync(
        string macAddress,
        UpsertDeviceRemoteSettingsRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceByMacAsync(macAddress, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var tenantDefaults = await _resolver.GetTenantDefaultsAsync(device.TenantId, cancellationToken);
        var settings = device.RemoteSettings;

        if (settings is null)
        {
            settings = CreateFromTenantDefaults(device.Id, tenantDefaults);
            _dbContext.DeviceRemoteSettings.Add(settings);
            device.RemoteSettings = settings;
        }

        ApplyRequest(settings, request);
        settings.InheritFromGroup = false;
        settings.SettingsVersion++;
        settings.PendingApply = true;
        settings.UpdatedUtc = DateTime.UtcNow;

        var applyMode = request.ApplyOnReboot ? "reboot" : "instant";
        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.General,
            settings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            "Admin updated device remote settings.",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(device.MacAddress, settings);
    }

    public async Task<DeviceRemoteSettingsDto?> PatchInheritanceAsync(
        string macAddress,
        PatchDeviceSettingsInheritanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceByMacAsync(macAddress, cancellationToken);
        if (device is null)
        {
            return null;
        }

        if (device.RemoteSettings is null)
        {
            var tenantDefaults = await _resolver.GetTenantDefaultsAsync(device.TenantId, cancellationToken);
            device.RemoteSettings = CreateFromTenantDefaults(device.Id, tenantDefaults);
            _dbContext.DeviceRemoteSettings.Add(device.RemoteSettings);
        }

        device.RemoteSettings.InheritFromGroup = request.InheritFromGroup;
        device.RemoteSettings.UpdatedUtc = DateTime.UtcNow;

        if (device.AgentAdvancedSettings is not null)
        {
            device.AgentAdvancedSettings.InheritFromGroup = request.InheritFromGroup;
            device.AgentAdvancedSettings.UpdatedUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(device.MacAddress, device.RemoteSettings);
    }

    public Task<EffectiveAgentSettings> ResolveEffectiveForDeviceAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default) =>
        _resolver.ResolveEffectiveGeneralAsync(deviceId, cancellationToken);

    public async Task<AgentConfigResponse?> GetAgentConfigAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceByMacAsync(macAddress, cancellationToken);
        if (device is null)
        {
            return null;
        }

        return await _resolver.BuildAgentConfigAsync(device, cancellationToken);
    }

    private async Task<Device?> FindDeviceByMacAsync(string macAddress, CancellationToken cancellationToken)
    {
        var normalizedMac = macAddress.Trim();
        return await _dbContext.Devices
            .Include(d => d.RemoteSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);
    }

    private static DeviceRemoteSettings CreateFromTenantDefaults(Guid deviceId, TenantAgentDefaults tenantDefaults)
    {
        var (host, port) = AgentSettingsHelper.ParseHostPort(tenantDefaults.ServerBaseUrl);
        return new DeviceRemoteSettings
        {
            DeviceId = deviceId,
            ServerHost = host,
            ServerPort = port,
            PollIntervalSeconds = tenantDefaults.DefaultPollIntervalSeconds,
            CommunicationType = tenantDefaults.DefaultCommunicationType,
            AgentEnabled = true,
            InheritFromGroup = true,
            SettingsVersion = 0,
            PendingApply = false,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private static void ApplyRequest(DeviceRemoteSettings settings, UpsertDeviceRemoteSettingsRequest request)
    {
        settings.ServerHost = request.ServerHost.Trim();
        settings.ServerPort = request.ServerPort;
        settings.PollIntervalSeconds = request.PollIntervalSeconds;
        settings.CommunicationType = request.CommunicationType;
        settings.AgentEnabled = request.AgentEnabled;
        settings.DesiredGroupName = request.DesiredGroupName;
        settings.AgentHostName = request.AgentHostName;
        settings.UseDhcpDiscovery = request.UseDhcpDiscovery;
        settings.ApplyOnReboot = request.ApplyOnReboot;
    }

    private static DeviceRemoteSettingsDto MapToDto(string macAddress, DeviceRemoteSettings settings) =>
        new()
        {
            MacAddress = macAddress,
            ServerHost = settings.ServerHost,
            ServerPort = settings.ServerPort,
            PollIntervalSeconds = settings.PollIntervalSeconds,
            CommunicationType = settings.CommunicationType,
            AgentEnabled = settings.AgentEnabled,
            DesiredGroupName = settings.DesiredGroupName,
            AgentHostName = settings.AgentHostName,
            UseDhcpDiscovery = settings.UseDhcpDiscovery,
            ApplyOnReboot = settings.ApplyOnReboot,
            InheritFromGroup = settings.InheritFromGroup,
            SettingsVersion = settings.SettingsVersion,
            PendingApply = settings.PendingApply,
            LastAppliedVersion = settings.LastAppliedVersion,
            LastAppliedUtc = settings.LastAppliedUtc,
            CreatedUtc = settings.CreatedUtc,
            UpdatedUtc = settings.UpdatedUtc
        };

    private static DeviceRemoteSettingsDto MapToDtoFromTenantDefaults(string macAddress, TenantAgentDefaults tenantDefaults)
    {
        var (host, port) = AgentSettingsHelper.ParseHostPort(tenantDefaults.ServerBaseUrl);
        return new DeviceRemoteSettingsDto
        {
            MacAddress = macAddress,
            ServerHost = host,
            ServerPort = port,
            PollIntervalSeconds = tenantDefaults.DefaultPollIntervalSeconds,
            CommunicationType = tenantDefaults.DefaultCommunicationType,
            AgentEnabled = true,
            InheritFromGroup = true,
            SettingsVersion = 0,
            PendingApply = false,
            CreatedUtc = tenantDefaults.UpdatedUtc,
            UpdatedUtc = tenantDefaults.UpdatedUtc
        };
    }
}
