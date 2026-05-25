using Intellinode.Application.Interfaces;
using Intellinode.Domain.Enums;
using Intellinode.Infrastructure.Options;
using Intellinode.Infrastructure.Persistence;
using Intellinode.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.NameTranslation;

namespace Intellinode.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AgentServerOptions>(configuration.GetSection(AgentServerOptions.SectionName));

        services.AddDbContext<IntellinodeDbContext>(options =>
            options
                .ConfigureWarnings(w =>
                    w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsql =>
                    {
                        npgsql.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            IntellinodeDbContext.SchemaName);
                        npgsql.MapEnum<EnrollmentState>(
                            "enrollment_state",
                            IntellinodeDbContext.SchemaName,
                            nameTranslator: new NpgsqlNullNameTranslator());
                        npgsql.MapEnum<HeartbeatBindingKind>(
                            "heartbeat_binding_kind",
                            IntellinodeDbContext.SchemaName,
                            nameTranslator: new NpgsqlNullNameTranslator());
                    })
                .UseSnakeCaseNamingConvention());

        services.AddSingleton<IAgentServerUrlProvider, AgentServerUrlProvider>();
        services.AddScoped<IIntellinodeDbContext>(sp => sp.GetRequiredService<IntellinodeDbContext>());
        services.AddScoped<IHeartbeatService, HeartbeatService>();
        services.AddScoped<AgentCredentialIssuer>();
        services.AddScoped<IAgentAuthService, AgentAuthService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAgentBootstrapService, AgentBootstrapService>();
        services.AddScoped<IAgentEnrollmentService, AgentEnrollmentService>();
        services.AddScoped<IAgentInventoryService, AgentInventoryService>();
        services.AddScoped<IAgentTaskService, AgentTaskService>();
        services.AddSingleton<ITokenService, TokenService>();
        return services;
    }
}
