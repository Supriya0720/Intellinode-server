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
        services.AddScoped<IAgentTaskService, AgentTaskService>();
        services.AddScoped<IDeviceRemoteSettingsService, DeviceRemoteSettingsService>();
        services.AddScoped<IDeviceAgentAdvancedSettingsService, DeviceAgentAdvancedSettingsService>();
        services.AddScoped<IGroupRemoteSettingsService, GroupRemoteSettingsService>();
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        services.AddScoped<IKeyboardSettingsService, KeyboardSettingsService>();
        services.AddScoped<EffectiveAgentSettingsResolver>();
        services.AddScoped<IEffectiveAgentSettingsResolver>(sp => sp.GetRequiredService<EffectiveAgentSettingsResolver>());
        services.AddSingleton<ITokenService, TokenService>();
        return services;
    }
}
