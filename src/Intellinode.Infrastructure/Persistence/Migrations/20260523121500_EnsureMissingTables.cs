using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class EnsureMissingTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS intellinode.agent_enrollment_tokens (
                id uuid NOT NULL,
                token_hash text NOT NULL,
                mac_address character varying(300),
                device_id uuid,
                created_by_admin_id uuid,
                expires_utc timestamp with time zone NOT NULL,
                consumed_utc timestamp with time zone,
                created_utc timestamp with time zone NOT NULL,
                CONSTRAINT pk_agent_enrollment_tokens PRIMARY KEY (id),
                CONSTRAINT fk_agent_enrollment_tokens_admin_users_created_by_admin_id
                    FOREIGN KEY (created_by_admin_id)
                    REFERENCES intellinode.admin_users (id)
                    ON DELETE SET NULL,
                CONSTRAINT fk_agent_enrollment_tokens_devices_device_id
                    FOREIGN KEY (device_id)
                    REFERENCES intellinode.devices (id)
                    ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS ix_agent_enrollment_tokens_created_by_admin_id
                ON intellinode.agent_enrollment_tokens (created_by_admin_id);

            CREATE INDEX IF NOT EXISTS ix_agent_enrollment_tokens_device_id
                ON intellinode.agent_enrollment_tokens (device_id);

            CREATE UNIQUE INDEX IF NOT EXISTS ix_agent_enrollment_tokens_token_hash
                ON intellinode.agent_enrollment_tokens (token_hash);

            CREATE TABLE IF NOT EXISTS intellinode.agent_refresh_tokens (
                id uuid NOT NULL,
                device_id uuid NOT NULL,
                token_hash text NOT NULL,
                expires_utc timestamp with time zone NOT NULL,
                created_utc timestamp with time zone NOT NULL,
                revoked_utc timestamp with time zone,
                CONSTRAINT pk_agent_refresh_tokens PRIMARY KEY (id),
                CONSTRAINT fk_agent_refresh_tokens_devices_device_id
                    FOREIGN KEY (device_id)
                    REFERENCES intellinode.devices (id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_agent_refresh_tokens_device_id
                ON intellinode.agent_refresh_tokens (device_id);

            CREATE INDEX IF NOT EXISTS ix_agent_refresh_tokens_token_hash
                ON intellinode.agent_refresh_tokens (token_hash);

            CREATE TABLE IF NOT EXISTS intellinode.device_inventory (
                device_id uuid NOT NULL,
                hardware jsonb,
                network jsonb,
                os_info jsonb,
                security jsonb,
                collected_utc timestamp with time zone NOT NULL,
                version integer NOT NULL,
                CONSTRAINT pk_device_inventory PRIMARY KEY (device_id),
                CONSTRAINT fk_device_inventory_devices_device_id
                    FOREIGN KEY (device_id)
                    REFERENCES intellinode.devices (id)
                    ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS intellinode.device_tasks (
                id uuid NOT NULL,
                device_id uuid NOT NULL,
                legacy_task_id integer NOT NULL,
                module_name character varying(128) NOT NULL,
                function_name character varying(128) NOT NULL,
                function_parameter character varying(512) NOT NULL,
                extra_data character varying(512) NOT NULL,
                status integer NOT NULL,
                created_utc timestamp with time zone NOT NULL,
                completed_utc timestamp with time zone,
                CONSTRAINT pk_device_tasks PRIMARY KEY (id),
                CONSTRAINT fk_device_tasks_devices_device_id
                    FOREIGN KEY (device_id)
                    REFERENCES intellinode.devices (id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_device_tasks_device_id
                ON intellinode.device_tasks (device_id);

            CREATE TABLE IF NOT EXISTS intellinode.heartbeat_binding_changes (
                id uuid NOT NULL,
                device_id uuid NOT NULL,
                is_service_mode boolean NOT NULL,
                status character varying(32) NOT NULL,
                changed_value character varying(512) NOT NULL,
                kind intellinode.heartbeat_binding_kind NOT NULL,
                is_binding_active boolean NOT NULL,
                created_utc timestamp with time zone NOT NULL,
                CONSTRAINT pk_heartbeat_binding_changes PRIMARY KEY (id),
                CONSTRAINT fk_heartbeat_binding_changes_devices_device_id
                    FOREIGN KEY (device_id)
                    REFERENCES intellinode.devices (id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_heartbeat_binding_changes_device_id
                ON intellinode.heartbeat_binding_changes (device_id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS intellinode.heartbeat_binding_changes;
            DROP TABLE IF EXISTS intellinode.device_tasks;
            DROP TABLE IF EXISTS intellinode.device_inventory;
            DROP TABLE IF EXISTS intellinode.agent_refresh_tokens;
            DROP TABLE IF EXISTS intellinode.agent_enrollment_tokens;
            """);
    }
}
