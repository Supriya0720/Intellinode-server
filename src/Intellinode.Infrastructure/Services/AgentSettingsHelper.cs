using Intellinode.Application.Contracts.Agents;
using Intellinode.Domain.Entities;
using Intellinode.Domain.Enums;

namespace Intellinode.Infrastructure.Services;

internal static class AgentSettingsHelper
{
    public static string BuildServerBaseUrl(string host, int port, CommunicationType communicationType)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var scheme = communicationType switch
        {
            CommunicationType.HTTP => "http",
            CommunicationType.HTTPS => "https",
            CommunicationType.TCP => "tcp",
            _ => "https"
        };

        return $"{scheme}://{host.Trim()}:{port}";
    }

    public static (string Host, int Port) ParseHostPort(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return (string.Empty, 443);
        }

        var port = uri.Port > 0
            ? uri.Port
            : uri.Scheme switch
            {
                "http" => 80,
                "https" => 443,
                _ => 443
            };

        return (uri.Host, port);
    }

    public static string NormalizeBaseUrl(string url) => url.TrimEnd('/');

    public static EffectiveAgentSettings BuildGeneralFromDeviceRow(
        DeviceRemoteSettings settings,
        TenantAgentDefaults tenantDefaults,
        long? lastAppliedVersion)
    {
        var serverBaseUrl = string.IsNullOrWhiteSpace(settings.ServerHost)
            ? tenantDefaults.ServerBaseUrl
            : BuildServerBaseUrl(settings.ServerHost, settings.ServerPort, settings.CommunicationType);

        var apiBaseUrl = string.IsNullOrWhiteSpace(settings.ServerHost)
            ? tenantDefaults.ApiBaseUrl
            : $"{NormalizeBaseUrl(serverBaseUrl)}/api/v1";

        return new EffectiveAgentSettings
        {
            ServerBaseUrl = serverBaseUrl,
            ApiBaseUrl = apiBaseUrl,
            PollIntervalSeconds = settings.PollIntervalSeconds,
            CommunicationType = settings.CommunicationType,
            AgentEnabled = settings.AgentEnabled,
            SettingsVersion = settings.SettingsVersion,
            PendingApply = settings.PendingApply || (lastAppliedVersion ?? 0) < settings.SettingsVersion,
            Source = "device"
        };
    }

    public static EffectiveAgentSettings BuildGeneralFromGroupRow(
        GroupRemoteSettings settings,
        TenantAgentDefaults tenantDefaults,
        long? lastAppliedVersion)
    {
        var serverBaseUrl = string.IsNullOrWhiteSpace(settings.ServerHost)
            ? tenantDefaults.ServerBaseUrl
            : BuildServerBaseUrl(settings.ServerHost, settings.ServerPort, settings.CommunicationType);

        var apiBaseUrl = string.IsNullOrWhiteSpace(settings.ServerHost)
            ? tenantDefaults.ApiBaseUrl
            : $"{NormalizeBaseUrl(serverBaseUrl)}/api/v1";

        return new EffectiveAgentSettings
        {
            ServerBaseUrl = serverBaseUrl,
            ApiBaseUrl = apiBaseUrl,
            PollIntervalSeconds = settings.PollIntervalSeconds,
            CommunicationType = settings.CommunicationType,
            AgentEnabled = settings.AgentEnabled,
            SettingsVersion = settings.SettingsVersion,
            PendingApply = (lastAppliedVersion ?? 0) < settings.SettingsVersion,
            Source = "group"
        };
    }

    public static EffectiveAgentSettings BuildGeneralFromTenant(TenantAgentDefaults tenantDefaults) =>
        new()
        {
            ServerBaseUrl = tenantDefaults.ServerBaseUrl,
            ApiBaseUrl = tenantDefaults.ApiBaseUrl,
            PollIntervalSeconds = tenantDefaults.DefaultPollIntervalSeconds,
            CommunicationType = tenantDefaults.DefaultCommunicationType,
            AgentEnabled = true,
            SettingsVersion = 0,
            PendingApply = false,
            Source = "tenant"
        };

    public static AgentAdvancedConfigDto MapAdvancedFromDevice(DeviceAgentAdvancedSettings settings, long? lastAppliedVersion) =>
        new()
        {
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
            AdvancedSettingsVersion = settings.SettingsVersion,
            AdvancedPendingApply = settings.PendingApply || (lastAppliedVersion ?? 0) < settings.SettingsVersion
        };

    public static AgentAdvancedConfigDto MapAdvancedFromGroup(GroupAgentAdvancedSettings settings, long? lastAppliedVersion) =>
        new()
        {
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
            AdvancedSettingsVersion = settings.SettingsVersion,
            AdvancedPendingApply = (lastAppliedVersion ?? 0) < settings.SettingsVersion
        };

    public static AgentAdvancedConfigDto CreateDefaultAdvanced(int defaultPollInterval) =>
        new()
        {
            DebugLevel = 0,
            HeartbeatIntervalSeconds = defaultPollInterval,
            ApplicationIntervalSeconds = 60,
            ConnectionType = CommunicationType.HTTPS,
            DhcpPollIntervalSeconds = defaultPollInterval,
            AdvancedSettingsVersion = 0,
            AdvancedPendingApply = false
        };

    public static void ApplyAdvancedRequest(DeviceAgentAdvancedSettings target, UpsertDeviceAgentAdvancedSettingsRequest request)
    {
        target.DebugLevel = request.DebugLevel;
        target.HeartbeatIntervalSeconds = request.HeartbeatIntervalSeconds;
        target.ApplicationIntervalSeconds = request.ApplicationIntervalSeconds;
        target.UsbLogsEnabled = request.UsbLogsEnabled;
        target.ApplicationLogsEnabled = request.ApplicationLogsEnabled;
        target.BootLogsEnabled = request.BootLogsEnabled;
        target.ScreensaverLogsEnabled = request.ScreensaverLogsEnabled;
        target.YumMonitorEnabled = request.YumMonitorEnabled;
        target.SignalrMonitoringEnabled = request.SignalrMonitoringEnabled;
        target.ConnectionType = request.ConnectionType;
        target.DhcpPollIntervalSeconds = request.DhcpPollIntervalSeconds;
        target.AlwaysApply = request.AlwaysApply;
        target.ApplyOnNextReboot = request.ApplyOnNextReboot;
        target.InheritFromGroup = request.InheritFromGroup;
        target.ExtraJson = request.ExtraJson;
    }

    public static void ApplyAdvancedRequest(GroupAgentAdvancedSettings target, UpsertGroupAgentAdvancedSettingsRequest request)
    {
        target.DebugLevel = request.DebugLevel;
        target.HeartbeatIntervalSeconds = request.HeartbeatIntervalSeconds;
        target.ApplicationIntervalSeconds = request.ApplicationIntervalSeconds;
        target.UsbLogsEnabled = request.UsbLogsEnabled;
        target.ApplicationLogsEnabled = request.ApplicationLogsEnabled;
        target.BootLogsEnabled = request.BootLogsEnabled;
        target.ScreensaverLogsEnabled = request.ScreensaverLogsEnabled;
        target.YumMonitorEnabled = request.YumMonitorEnabled;
        target.SignalrMonitoringEnabled = request.SignalrMonitoringEnabled;
        target.ConnectionType = request.ConnectionType;
        target.DhcpPollIntervalSeconds = request.DhcpPollIntervalSeconds;
        target.AlwaysApply = request.AlwaysApply;
        target.ApplyOnNextReboot = request.ApplyOnNextReboot;
    }

    public static DeviceAgentAdvancedSettings CreateDefaultDeviceAdvanced(Guid deviceId, int defaultPollInterval) =>
        new()
        {
            DeviceId = deviceId,
            HeartbeatIntervalSeconds = defaultPollInterval,
            ApplicationIntervalSeconds = 60,
            DhcpPollIntervalSeconds = defaultPollInterval,
            SettingsVersion = 0,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

    public static GroupAgentAdvancedSettings CreateDefaultGroupAdvanced(Guid groupId, int defaultPollInterval) =>
        new()
        {
            GroupId = groupId,
            HeartbeatIntervalSeconds = defaultPollInterval,
            ApplicationIntervalSeconds = 60,
            DhcpPollIntervalSeconds = defaultPollInterval,
            SettingsVersion = 0,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
}
