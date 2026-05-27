using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAgentEnrollmentTokenPlatform : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$ BEGIN
                CREATE TYPE intellinode.agent_platform AS ENUM ('Windows', 'Linux');
            EXCEPTION
                WHEN duplicate_object THEN null;
            END $$;

            ALTER TABLE intellinode.agent_enrollment_tokens
                ADD COLUMN IF NOT EXISTS platform intellinode.agent_platform NOT NULL DEFAULT 'Windows';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE intellinode.agent_enrollment_tokens
                DROP COLUMN IF EXISTS platform;

            DROP TYPE IF EXISTS intellinode.agent_platform;
            """);
    }
}
