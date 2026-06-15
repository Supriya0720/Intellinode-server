using Intellinode.Application.Interfaces;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Intellinode.Infrastructure.Services.DeviceManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Intellinode.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AgentServerOptions>(configuration.GetSection(AgentServerOptions.SectionName));
        services.Configure<AgentDiscoveryOptions>(configuration.GetSection(AgentDiscoveryOptions.SectionName));
        services.Configure<SystemSettingOptions>(configuration.GetSection(SystemSettingOptions.SectionName));
        services.Configure<KeyboardOptions>(configuration.GetSection(KeyboardOptions.SectionName));
        services.Configure<MouseOptions>(configuration.GetSection(MouseOptions.SectionName));
        services.Configure<DisplayOptions>(configuration.GetSection(DisplayOptions.SectionName));
        services.Configure<Windows8021xOptions>(configuration.GetSection(Windows8021xOptions.SectionName));
        services.Configure<WindowsComputerNameOptions>(configuration.GetSection(WindowsComputerNameOptions.SectionName));
        services.Configure<WindowsEthernetSetupOptions>(configuration.GetSection(WindowsEthernetSetupOptions.SectionName));
        services.Configure<WindowsWirelessSetupOptions>(configuration.GetSection(WindowsWirelessSetupOptions.SectionName));
        services.Configure<WindowsWirelessPropertiesOptions>(configuration.GetSection(WindowsWirelessPropertiesOptions.SectionName));
        services.Configure<TimeAndLanguageReferenceOptions>(configuration.GetSection(TimeAndLanguageReferenceOptions.SectionName));
        services.Configure<WindowsDateTimeOptions>(configuration.GetSection(WindowsDateTimeOptions.SectionName));
        services.Configure<WindowsRegionLocationOptions>(configuration.GetSection(WindowsRegionLocationOptions.SectionName));
        services.Configure<WindowsRegionalFormatOptions>(configuration.GetSection(WindowsRegionalFormatOptions.SectionName));
        services.Configure<PowerManagementReferenceOptions>(configuration.GetSection(PowerManagementReferenceOptions.SectionName));
        services.Configure<WindowsPowerManagementOptions>(configuration.GetSection(WindowsPowerManagementOptions.SectionName));
        services.Configure<WindowsScreenSaverOptions>(configuration.GetSection(WindowsScreenSaverOptions.SectionName));
        services.Configure<WindowsTaskbarOptions>(configuration.GetSection(WindowsTaskbarOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddSingleton(IntellinodeNpgsqlConfiguration.BuildDataSource(connectionString));

        services.AddDbContext<IntellinodeDbContext>(
            (sp, options) => IntellinodeNpgsqlConfiguration.ConfigureDbContextOptions(
                options,
                sp.GetRequiredService<NpgsqlDataSource>()),
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddSingleton<IAgentServerUrlProvider, AgentServerUrlProvider>();
        services.AddScoped<IIntellinodeDbContext>(sp => sp.GetRequiredService<IntellinodeDbContext>());
        services.AddScoped<IHeartbeatService, HeartbeatService>();
        services.AddScoped<AgentCredentialIssuer>();
        services.AddScoped<IAgentAuthService, AgentAuthService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAgentBootstrapService, AgentBootstrapService>();
        services.AddScoped<IAgentEnrollmentService, AgentEnrollmentService>();
        services.AddScoped<EnrollmentCoreService>();
        services.AddScoped<IWindowsAgentEnrollmentService, WindowsAgentEnrollmentService>();
        services.AddScoped<IAgentInventoryService, AgentInventoryService>();
        services.AddScoped<IDiscoverLookupWriter, DiscoverLookupWriter>();
        services.AddScoped<IDiscoverLookupService, DiscoverLookupService>();
        services.AddScoped<IDeviceManagerService, DeviceManagerService>();
        services.AddScoped<IDeviceManagerRootsService, DeviceManagerRootsService>();
        services.AddScoped<IDeviceManagerGroupChildrenService, DeviceManagerGroupChildrenService>();
        services.AddScoped<IDeviceManagerGroupDevicesService, DeviceManagerGroupDevicesService>();
        services.AddScoped<IAgentCommunicationLogWriter, AgentCommunicationLogWriter>();
        services.AddScoped<IExceptionLogWriter, ExceptionLogWriter>();
        services.AddScoped<KeyboardTaskAckHandler>();
        services.AddScoped<MouseTaskAckHandler>();
        services.AddScoped<DisplayTaskAckHandler>();
        services.AddScoped<Windows8021xTaskAckHandler>();
        services.AddScoped<WindowsComputerNameTaskAckHandler>();
        services.AddScoped<WindowsDateTimeTaskAckHandler>();
        services.AddScoped<WindowsRegionLocationTaskAckHandler>();
        services.AddScoped<WindowsRegionalFormatTaskAckHandler>();
        services.AddScoped<WindowsEthernetSetupTaskAckHandler>();
        services.AddScoped<WindowsWirelessSetupTaskAckHandler>();
        services.AddScoped<WindowsWirelessPropertiesTaskAckHandler>();
        services.AddScoped<WindowsPowerManagementTaskAckHandler>();
        services.AddScoped<WindowsScreenSaverTaskAckHandler>();
        services.AddScoped<WindowsTaskbarTaskAckHandler>();
        services.AddScoped<AgentTaskService>();
        services.AddScoped<IAgentTaskService, ScreenSaverHydratingAgentTaskService>();
        services.AddScoped<IDeviceRemoteSettingsService, DeviceRemoteSettingsService>();
        services.AddScoped<IDeviceAgentAdvancedSettingsService, DeviceAgentAdvancedSettingsService>();
        services.AddScoped<IGroupRemoteSettingsService, GroupRemoteSettingsService>();
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<IKeyboardSettingsService, KeyboardSettingsService>();
        services.AddScoped<IMouseSettingsService, MouseSettingsService>();
        services.AddScoped<IDisplaySettingsService, DisplaySettingsService>();
        services.AddScoped<IWindows8021xSettingsService, Windows8021xSettingsService>();
        services.AddScoped<IWindows8021xPayloadBuilder, Windows8021xPayloadBuilder>();
        services.AddScoped<IWindows8021xTaskPayloadHydrator, Windows8021xTaskPayloadHydrator>();
        services.AddScoped<IWindowsWirelessPropertiesPayloadBuilder, WindowsWirelessPropertiesPayloadBuilder>();
        services.AddScoped<IWindowsWirelessPropertiesTaskPayloadHydrator, WindowsWirelessPropertiesTaskPayloadHydrator>();
        services.AddScoped<IWindowsWirelessPropertiesSettingsService, WindowsWirelessPropertiesSettingsService>();
        services.AddScoped<IWindowsComputerNamePayloadBuilder, WindowsComputerNamePayloadBuilder>();
        services.AddScoped<IWindowsComputerNameSettingsService, WindowsComputerNameSettingsService>();
        services.AddScoped<IWindowsDateTimePayloadBuilder, WindowsDateTimePayloadBuilder>();
        services.AddScoped<IWindowsDateTimeSettingsService, WindowsDateTimeSettingsService>();
        services.AddScoped<IWindowsRegionLocationPayloadBuilder, WindowsRegionLocationPayloadBuilder>();
        services.AddScoped<IWindowsRegionLocationSettingsService, WindowsRegionLocationSettingsService>();
        services.AddScoped<IWindowsRegionalFormatPayloadBuilder, WindowsRegionalFormatPayloadBuilder>();
        services.AddScoped<IWindowsRegionalFormatSettingsService, WindowsRegionalFormatSettingsService>();
        services.AddScoped<IWindowsEthernetSetupSettingsService, WindowsEthernetSetupSettingsService>();
        services.AddScoped<IWindowsEthernetSetupPayloadBuilder, WindowsEthernetSetupPayloadBuilder>();
        services.AddScoped<IWindowsWirelessSetupSettingsService, WindowsWirelessSetupSettingsService>();
        services.AddScoped<IWindowsWirelessSetupPayloadBuilder, WindowsWirelessSetupPayloadBuilder>();
        services.AddScoped<ITimeAndLanguageReferenceService, TimeAndLanguageReferenceService>();
        services.AddScoped<IPowerManagementReferenceService, PowerManagementReferenceService>();
        services.AddScoped<IWindowsPowerManagementPayloadBuilder, WindowsPowerManagementPayloadBuilder>();
        services.AddScoped<IWindowsPowerManagementTaskPayloadHydrator, WindowsPowerManagementTaskPayloadHydrator>();
        services.AddScoped<IWindowsPowerManagementSettingsService, WindowsPowerManagementSettingsService>();
        services.AddScoped<IWindowsScreenSaverPayloadBuilder, WindowsScreenSaverPayloadBuilder>();
        services.AddScoped<IWindowsScreenSaverTaskPayloadHydrator, WindowsScreenSaverTaskPayloadHydrator>();
        services.AddScoped<IWindowsScreenSaverSettingsService, WindowsScreenSaverSettingsService>();
        services.AddScoped<IWindowsTaskbarPayloadBuilder, WindowsTaskbarPayloadBuilder>();
        services.AddScoped<IWindowsTaskbarSettingsService, WindowsTaskbarSettingsService>();
        services.AddScoped<EffectiveAgentSettingsResolver>();
        services.AddScoped<IEffectiveAgentSettingsResolver>(sp => sp.GetRequiredService<EffectiveAgentSettingsResolver>());
        services.AddSingleton<ITokenService, TokenService>();
        return services;
    }
}
