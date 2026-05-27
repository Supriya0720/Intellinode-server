using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql.NameTranslation;

namespace Intellinode.Infrastructure.Persistence;

public sealed class IntellinodeDbContextFactory : IDesignTimeDbContextFactory<IntellinodeDbContext>
{
    public IntellinodeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../Intellinode.Api"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<IntellinodeDbContext>();
        optionsBuilder
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
                    npgsql.MapEnum<AgentPlatform>(
                        "agent_platform",
                        IntellinodeDbContext.SchemaName,
                        nameTranslator: new NpgsqlNullNameTranslator());
                    npgsql.MapEnum<CommunicationType>(
                        "communication_type",
                        IntellinodeDbContext.SchemaName,
                        nameTranslator: new NpgsqlNullNameTranslator());
                    npgsql.MapEnum<SettingsKind>(
                        "settings_kind",
                        IntellinodeDbContext.SchemaName,
                        nameTranslator: new NpgsqlNullNameTranslator());
                    npgsql.MapEnum<SettingsApplyStatus>(
                        "settings_apply_status",
                        IntellinodeDbContext.SchemaName,
                        nameTranslator: new NpgsqlNullNameTranslator());
                })
            .UseSnakeCaseNamingConvention();

        return new IntellinodeDbContext(optionsBuilder.Options);
    }
}
