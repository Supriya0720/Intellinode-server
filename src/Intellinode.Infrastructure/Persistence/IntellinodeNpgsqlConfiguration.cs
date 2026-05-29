using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;

namespace Intellinode.Infrastructure.Persistence;

internal static class IntellinodeNpgsqlConfiguration
{
    internal static readonly NpgsqlNullNameTranslator NameTranslator = new();

    internal static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        MapEnums(builder);
        return builder.Build();
    }

    internal static void ConfigureDbContextOptions(
        DbContextOptionsBuilder optionsBuilder,
        NpgsqlDataSource dataSource)
    {
        ConfigureDbContextOptions(
            (DbContextOptionsBuilder<IntellinodeDbContext>)optionsBuilder,
            dataSource);
    }

    internal static void ConfigureDbContextOptions(
        DbContextOptionsBuilder<IntellinodeDbContext> optionsBuilder,
        NpgsqlDataSource dataSource)
    {
        optionsBuilder
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .UseNpgsql(dataSource, ConfigureNpgsql)
            .UseSnakeCaseNamingConvention();
    }

    internal static void ConfigureDbContextOptions(
        DbContextOptionsBuilder<IntellinodeDbContext> optionsBuilder,
        string connectionString)
    {
        optionsBuilder
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .UseNpgsql(connectionString, ConfigureNpgsql)
            .UseSnakeCaseNamingConvention();
    }

    private static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql)
    {
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", IntellinodeDbContext.SchemaName);
        MapEnums(npgsql);
    }

    private static void MapEnums(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<EnrollmentState>(PgEnumName("enrollment_state"), NameTranslator);
        builder.MapEnum<DiscoverLookupStatus>(PgEnumName("discover_lookup_status"), NameTranslator);
        builder.MapEnum<HeartbeatBindingKind>(PgEnumName("heartbeat_binding_kind"), NameTranslator);
        builder.MapEnum<AgentPlatform>(PgEnumName("agent_platform"), NameTranslator);
        builder.MapEnum<CommunicationType>(PgEnumName("communication_type"), NameTranslator);
        builder.MapEnum<SettingsKind>(PgEnumName("settings_kind"), NameTranslator);
        builder.MapEnum<SettingsApplyStatus>(PgEnumName("settings_apply_status"), NameTranslator);
    }

    private static void MapEnums(NpgsqlDbContextOptionsBuilder npgsql)
    {
        npgsql.MapEnum<EnrollmentState>("enrollment_state", IntellinodeDbContext.SchemaName, nameTranslator: NameTranslator);
        npgsql.MapEnum<DiscoverLookupStatus>("discover_lookup_status", IntellinodeDbContext.SchemaName, nameTranslator: NameTranslator);
        npgsql.MapEnum<HeartbeatBindingKind>("heartbeat_binding_kind", IntellinodeDbContext.SchemaName, nameTranslator: NameTranslator);
        npgsql.MapEnum<AgentPlatform>("agent_platform", IntellinodeDbContext.SchemaName, nameTranslator: NameTranslator);
        npgsql.MapEnum<CommunicationType>("communication_type", IntellinodeDbContext.SchemaName, nameTranslator: NameTranslator);
        npgsql.MapEnum<SettingsKind>("settings_kind", IntellinodeDbContext.SchemaName, nameTranslator: NameTranslator);
        npgsql.MapEnum<SettingsApplyStatus>("settings_apply_status", IntellinodeDbContext.SchemaName, nameTranslator: NameTranslator);
    }

    private static string PgEnumName(string enumName) =>
        $"{IntellinodeDbContext.SchemaName}.{enumName}";
}
