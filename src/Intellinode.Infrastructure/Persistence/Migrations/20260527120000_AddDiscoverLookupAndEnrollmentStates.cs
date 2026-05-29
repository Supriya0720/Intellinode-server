using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDiscoverLookupAndEnrollmentStates : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
            .Annotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
            .Annotation("Npgsql:Enum:discover_lookup_status.intellinode", "Pending,Approved,Rejected")
            .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled,PendingApproval,Rejected")
            .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
            .Annotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
            .Annotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced")
            .Annotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
            .Annotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
            .Annotation("Npgsql:Enum:intellinode.discover_lookup_status", "Approved,Pending,Rejected")
            .Annotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingApproval,PendingInventory,Rejected,Unlicensed")
            .Annotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
            .Annotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
            .Annotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,General")
            .OldAnnotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
            .OldAnnotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
            .OldAnnotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
            .OldAnnotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
            .OldAnnotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
            .OldAnnotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced")
            .OldAnnotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
            .OldAnnotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
            .OldAnnotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingInventory,Unlicensed")
            .OldAnnotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
            .OldAnnotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
            .OldAnnotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,General");

        migrationBuilder.Sql(
            """
            ALTER TYPE intellinode.enrollment_state ADD VALUE IF NOT EXISTS 'PendingApproval';
            ALTER TYPE intellinode.enrollment_state ADD VALUE IF NOT EXISTS 'Rejected';

            DO $$ BEGIN
                CREATE TYPE intellinode.discover_lookup_status AS ENUM ('Pending', 'Approved', 'Rejected');
            EXCEPTION
                WHEN duplicate_object THEN null;
            END $$;

            CREATE TABLE IF NOT EXISTS intellinode.discover_lookup (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                tenant_id uuid NOT NULL,
                device_id uuid,
                mac_address character varying(300) NOT NULL,
                host_name character varying(255) NOT NULL DEFAULT '',
                ip_address character varying(64) NOT NULL DEFAULT '',
                domain character varying(255) NOT NULL DEFAULT '',
                os_name character varying(64) NOT NULL DEFAULT '',
                os_version character varying(64) NOT NULL DEFAULT '',
                agent_version character varying(64) NOT NULL DEFAULT '',
                discovery_type character varying(64) NOT NULL DEFAULT 'AgentSelfDiscovery',
                status intellinode.discover_lookup_status NOT NULL DEFAULT 'Pending',
                discovered_utc timestamp with time zone NOT NULL DEFAULT NOW(),
                updated_utc timestamp with time zone NOT NULL DEFAULT NOW(),
                approved_by_admin_id uuid,
                approved_utc timestamp with time zone,
                rejected_by_admin_id uuid,
                rejected_utc timestamp with time zone,
                rejection_reason character varying(500),
                notes character varying(1000),
                CONSTRAINT pk_discover_lookup PRIMARY KEY (id)
            );

            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS device_id uuid;
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS host_name character varying(255) NOT NULL DEFAULT '';
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS ip_address character varying(64) NOT NULL DEFAULT '';
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS domain character varying(255) NOT NULL DEFAULT '';
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS os_name character varying(64) NOT NULL DEFAULT '';
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS os_version character varying(64) NOT NULL DEFAULT '';
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS agent_version character varying(64) NOT NULL DEFAULT '';
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS discovery_type character varying(64) NOT NULL DEFAULT 'AgentSelfDiscovery';
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS discovered_utc timestamp with time zone NOT NULL DEFAULT NOW();
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS approved_by_admin_id uuid;
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS approved_utc timestamp with time zone;
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS rejected_by_admin_id uuid;
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS rejected_utc timestamp with time zone;
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS rejection_reason character varying(500);
            ALTER TABLE intellinode.discover_lookup ADD COLUMN IF NOT EXISTS notes character varying(1000);

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                      FROM information_schema.columns
                     WHERE table_schema = 'intellinode'
                       AND table_name = 'discover_lookup'
                       AND column_name = 'status'
                ) THEN
                    ALTER TABLE intellinode.discover_lookup
                        ADD COLUMN status intellinode.discover_lookup_status NOT NULL DEFAULT 'Pending';
                END IF;
            END $$;

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                      FROM information_schema.columns
                     WHERE table_schema = 'intellinode'
                       AND table_name = 'discover_lookup'
                       AND column_name = 'created_utc'
                ) THEN
                    UPDATE intellinode.discover_lookup
                       SET discovered_utc = created_utc
                     WHERE discovered_utc IS NULL
                        OR discovered_utc = TIMESTAMPTZ '1970-01-01 00:00:00+00';
                END IF;
            END $$;

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                      FROM information_schema.columns
                     WHERE table_schema = 'intellinode'
                       AND table_name = 'discover_lookup'
                       AND column_name = 'lookup_status'
                ) THEN
                    UPDATE intellinode.discover_lookup
                       SET status = CASE lookup_status
                           WHEN 'Registered' THEN 'Approved'::intellinode.discover_lookup_status
                           WHEN 'Rejected' THEN 'Rejected'::intellinode.discover_lookup_status
                           ELSE 'Pending'::intellinode.discover_lookup_status
                       END;

                    ALTER TABLE intellinode.discover_lookup DROP COLUMN lookup_status;
                END IF;
            END $$;

            ALTER TABLE intellinode.discover_lookup DROP COLUMN IF EXISTS created_utc;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                      FROM pg_constraint c
                      JOIN pg_class t ON c.conrelid = t.oid
                      JOIN pg_namespace n ON t.relnamespace = n.oid
                      JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (c.conkey)
                     WHERE n.nspname = 'intellinode'
                       AND t.relname = 'discover_lookup'
                       AND c.contype = 'f'
                       AND a.attname = 'tenant_id'
                ) THEN
                    ALTER TABLE intellinode.discover_lookup
                        ADD CONSTRAINT fk_discover_lookup_tenants_tenant_id
                        FOREIGN KEY (tenant_id) REFERENCES intellinode.tenants (id) ON DELETE CASCADE;
                END IF;
            END $$;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                      FROM pg_constraint c
                      JOIN pg_class t ON c.conrelid = t.oid
                      JOIN pg_namespace n ON t.relnamespace = n.oid
                      JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (c.conkey)
                     WHERE n.nspname = 'intellinode'
                       AND t.relname = 'discover_lookup'
                       AND c.contype = 'f'
                       AND a.attname = 'device_id'
                ) THEN
                    ALTER TABLE intellinode.discover_lookup
                        ADD CONSTRAINT fk_discover_lookup_devices_device_id
                        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE SET NULL;
                END IF;
            END $$;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                      FROM pg_constraint c
                      JOIN pg_class t ON c.conrelid = t.oid
                      JOIN pg_namespace n ON t.relnamespace = n.oid
                      JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (c.conkey)
                     WHERE n.nspname = 'intellinode'
                       AND t.relname = 'discover_lookup'
                       AND c.contype = 'f'
                       AND a.attname = 'approved_by_admin_id'
                ) THEN
                    ALTER TABLE intellinode.discover_lookup
                        ADD CONSTRAINT fk_discover_lookup_admin_users_approved_by_admin_id
                        FOREIGN KEY (approved_by_admin_id) REFERENCES intellinode.admin_users (id) ON DELETE SET NULL;
                END IF;
            END $$;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                      FROM pg_constraint c
                      JOIN pg_class t ON c.conrelid = t.oid
                      JOIN pg_namespace n ON t.relnamespace = n.oid
                      JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY (c.conkey)
                     WHERE n.nspname = 'intellinode'
                       AND t.relname = 'discover_lookup'
                       AND c.contype = 'f'
                       AND a.attname = 'rejected_by_admin_id'
                ) THEN
                    ALTER TABLE intellinode.discover_lookup
                        ADD CONSTRAINT fk_discover_lookup_admin_users_rejected_by_admin_id
                        FOREIGN KEY (rejected_by_admin_id) REFERENCES intellinode.admin_users (id) ON DELETE SET NULL;
                END IF;
            END $$;

            CREATE UNIQUE INDEX IF NOT EXISTS ix_discover_lookup_tenant_id_mac_address
                ON intellinode.discover_lookup (tenant_id, mac_address);

            CREATE INDEX IF NOT EXISTS ix_discover_lookup_tenant_id_status_discovered_utc
                ON intellinode.discover_lookup (tenant_id, status, discovered_utc);

            CREATE INDEX IF NOT EXISTS ix_discover_lookup_device_id
                ON intellinode.discover_lookup (device_id);

            CREATE INDEX IF NOT EXISTS ix_discover_lookup_approved_by_admin_id
                ON intellinode.discover_lookup (approved_by_admin_id);

            CREATE INDEX IF NOT EXISTS ix_discover_lookup_rejected_by_admin_id
                ON intellinode.discover_lookup (rejected_by_admin_id);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP TABLE IF EXISTS intellinode.discover_lookup;
            DROP TYPE IF EXISTS intellinode.discover_lookup_status;
            """);

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
            .Annotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
            .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
            .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
            .Annotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
            .Annotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced")
            .Annotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
            .Annotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
            .Annotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingInventory,Unlicensed")
            .Annotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
            .Annotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
            .Annotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,General")
            .OldAnnotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
            .OldAnnotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
            .OldAnnotation("Npgsql:Enum:discover_lookup_status.intellinode", "Pending,Approved,Rejected")
            .OldAnnotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled,PendingApproval,Rejected")
            .OldAnnotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
            .OldAnnotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
            .OldAnnotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced")
            .OldAnnotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
            .OldAnnotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
            .OldAnnotation("Npgsql:Enum:intellinode.discover_lookup_status", "Approved,Pending,Rejected")
            .OldAnnotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingApproval,PendingInventory,Rejected,Unlicensed")
            .OldAnnotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
            .OldAnnotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
            .OldAnnotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,General");
    }
}
