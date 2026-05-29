using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddExceptionLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS intellinode.exception_logs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                source character varying(256) NOT NULL,
                message text NOT NULL,
                stack_trace text,
                request_path character varying(512),
                http_method character varying(16),
                device_id uuid,
                admin_id uuid,
                logged_utc timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT pk_exception_logs PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS ix_exception_logs_logged_utc
                ON intellinode.exception_logs (logged_utc DESC);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS intellinode.exception_logs;");
    }
}
