using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Intellinode.Infrastructure.Persistence;

internal static class MigrationBootstrapper
{
    private const string InitialMigrationId = "20260523055049_InitialIntellinodeSchema";
    private const string ProductVersion = "10.0.1";

    private static readonly string[] RequiredTables =
    [
        "tenants",
        "device_groups",
        "devices",
        "admin_users",
        "agent_enrollment_tokens",
        "agent_refresh_tokens",
        "device_inventory",
        "device_tasks",
        "heartbeat_binding_changes"
    ];

    public static async Task EnsureAppliedAsync(
        IntellinodeDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE SCHEMA IF NOT EXISTS intellinode",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS intellinode."__EFMigrationsHistory" (
                migration_id character varying(150) NOT NULL PRIMARY KEY,
                product_version character varying(32) NOT NULL
            )
            """,
            cancellationToken);

        if (await AnyRequiredTableExistsAsync(dbContext, cancellationToken) &&
            !await AllRequiredTablesExistAsync(dbContext, cancellationToken) &&
            await MigrationAppliedAsync(dbContext, InitialMigrationId, cancellationToken))
        {
            logger.LogWarning(
                "Partial intellinode schema detected while {MigrationId} is marked applied. " +
                "Pending EnsureMissingTables migration will repair missing tables.",
                InitialMigrationId);
        }

        if (await AllRequiredTablesExistAsync(dbContext, cancellationToken) &&
            !await MigrationAppliedAsync(dbContext, InitialMigrationId, cancellationToken))
        {
            logger.LogInformation(
                "Intellinode tables already exist; baselining migration {MigrationId}.",
                InitialMigrationId);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO intellinode."__EFMigrationsHistory" (migration_id, product_version)
                 VALUES ({InitialMigrationId}, {ProductVersion})
                 ON CONFLICT (migration_id) DO NOTHING
                 """,
                cancellationToken);
        }

        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying pending EF migrations: {Migrations}", string.Join(", ", pending));
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> AnyRequiredTableExistsAsync(
        IntellinodeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        foreach (var table in RequiredTables)
        {
            if (await TableExistsAsync(dbContext, table, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> AllRequiredTablesExistAsync(
        IntellinodeDbContext dbContext,
        CancellationToken cancellationToken)
    {
        foreach (var table in RequiredTables)
        {
            if (!await TableExistsAsync(dbContext, table, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> TableExistsAsync(
        IntellinodeDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.Database
            .SqlQuery<ScalarResult>(
                $"""
                 SELECT EXISTS (
                     SELECT 1
                     FROM information_schema.tables
                     WHERE table_schema = 'intellinode'
                       AND table_name = {tableName}
                 ) AS value
                 """)
            .SingleAsync(cancellationToken);

        return result.Value;
    }

    private static async Task<bool> MigrationAppliedAsync(
        IntellinodeDbContext dbContext,
        string migrationId,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.Database
            .SqlQuery<ScalarResult>(
                $"""
                 SELECT EXISTS (
                     SELECT 1
                     FROM intellinode."__EFMigrationsHistory"
                     WHERE migration_id = {migrationId}
                 ) AS value
                 """)
            .SingleAsync(cancellationToken);

        return result.Value;
    }

    private sealed record ScalarResult(bool Value);
}
