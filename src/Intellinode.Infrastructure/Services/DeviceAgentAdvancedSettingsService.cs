using Intellinode.Application.Contracts.Agents;
using Intellinode.Application.Interfaces;
using Intellinode.Domain;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Intellinode.Infrastructure.Services;

public sealed class DeviceAgentAdvancedSettingsService : IDeviceAgentAdvancedSettingsService
{
    private readonly IntellinodeDbContext _dbContext;
    private readonly EffectiveAgentSettingsResolver _resolver;

    public DeviceAgentAdvancedSettingsService(
        IntellinodeDbContext dbContext,
        EffectiveAgentSettingsResolver resolver)
    {
        _dbContext = dbContext;
        _resolver = resolver;
    }

    public async Task<DeviceAgentAdvancedSettingsDto?> GetByMacAsync(
        string macAddress,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceByMacAsync(macAddress, cancellationToken);
        if (device is null)
        {
            return null;
        }

        if (device.AgentAdvancedSettings is not null)
        {
            return MapToDto(device.MacAddress, device.AgentAdvancedSettings);
        }

        var tenant = await _resolver.GetTenantDefaultsAsync(device.TenantId, cancellationToken);
        var defaults = AgentSettingsHelper.CreateDefaultAdvanced(tenant.DefaultPollIntervalSeconds);
        return new DeviceAgentAdvancedSettingsDto
        {
            MacAddress = device.MacAddress,
            DebugLevel = defaults.DebugLevel,
            HeartbeatIntervalSeconds = defaults.HeartbeatIntervalSeconds,
            ApplicationIntervalSeconds = defaults.ApplicationIntervalSeconds,
            ConnectionType = defaults.ConnectionType,
            DhcpPollIntervalSeconds = defaults.DhcpPollIntervalSeconds,
            InheritFromGroup = true,
            AdvancedSettingsVersion = 0,
            AdvancedPendingApply = false,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    public async Task<DeviceAgentAdvancedSettingsDto?> UpsertByMacAsync(
        string macAddress,
        UpsertDeviceAgentAdvancedSettingsRequest request,
        Guid? adminId = null,
        CancellationToken cancellationToken = default)
    {
        var device = await FindDeviceByMacAsync(macAddress, cancellationToken);
        if (device is null)
        {
            return null;
        }

        var tenant = await _resolver.GetTenantDefaultsAsync(device.TenantId, cancellationToken);
        var settings = device.AgentAdvancedSettings;

        if (settings is null)
        {
            settings = AgentSettingsHelper.CreateDefaultDeviceAdvanced(device.Id, tenant.DefaultPollIntervalSeconds);
            _dbContext.DeviceAgentAdvancedSettings.Add(settings);
            device.AgentAdvancedSettings = settings;
        }

        AgentSettingsHelper.ApplyAdvancedRequest(settings, request);
        settings.InheritFromGroup = false;
        settings.SettingsVersion++;
        settings.PendingApply = true;
        settings.UpdatedUtc = DateTime.UtcNow;

        var applyMode = request.ApplyOnNextReboot ? "reboot" : "instant";
        await _resolver.WriteApplyLogAsync(
            device.Id,
            SettingsKind.Advanced,
            settings.SettingsVersion,
            applyMode,
            SettingsApplyStatus.Pending,
            adminId,
            "Admin updated device advanced settings.",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(device.MacAddress, settings);
    }

    private async Task<Device?> FindDeviceByMacAsync(string macAddress, CancellationToken cancellationToken)
    {
        var normalizedMac = macAddress.Trim();
        return await _dbContext.Devices
            .Include(d => d.AgentAdvancedSettings)
            .FirstOrDefaultAsync(
                d => d.TenantId == TenantDefaults.DefaultTenantId && d.MacAddress == normalizedMac,
                cancellationToken);
    }

    private static DeviceAgentAdvancedSettingsDto MapToDto(string macAddress, DeviceAgentAdvancedSettings settings)
    {
        var advanced = AgentSettingsHelper.MapAdvancedFromDevice(settings, settings.LastAppliedVersion);
        return new DeviceAgentAdvancedSettingsDto
        {
            MacAddress = macAddress,
            DebugLevel = advanced.DebugLevel,
            HeartbeatIntervalSeconds = advanced.HeartbeatIntervalSeconds,
            ApplicationIntervalSeconds = advanced.ApplicationIntervalSeconds,
            UsbLogsEnabled = advanced.UsbLogsEnabled,
            ApplicationLogsEnabled = advanced.ApplicationLogsEnabled,
            BootLogsEnabled = advanced.BootLogsEnabled,
            ScreensaverLogsEnabled = advanced.ScreensaverLogsEnabled,
            YumMonitorEnabled = advanced.YumMonitorEnabled,
            SignalrMonitoringEnabled = advanced.SignalrMonitoringEnabled,
            ConnectionType = advanced.ConnectionType,
            DhcpPollIntervalSeconds = advanced.DhcpPollIntervalSeconds,
            AlwaysApply = advanced.AlwaysApply,
            ApplyOnNextReboot = advanced.ApplyOnNextReboot,
            InheritFromGroup = settings.InheritFromGroup,
            AdvancedSettingsVersion = settings.SettingsVersion,
            AdvancedPendingApply = settings.PendingApply,
            LastAppliedVersion = settings.LastAppliedVersion,
            LastAppliedUtc = settings.LastAppliedUtc,
            ExtraJson = settings.ExtraJson,
            CreatedUtc = settings.CreatedUtc,
            UpdatedUtc = settings.UpdatedUtc
        };
    }
}
