using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class BackfillDiscoverLookupForActiveDevices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS intellinode.agent_communication_logs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                device_id uuid,
                mac_address character varying(300),
                direction character varying(16) NOT NULL,
                endpoint character varying(256) NOT NULL,
                payload_summary text,
                command_code character varying(16),
                created_utc timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT pk_agent_communication_logs PRIMARY KEY (id)
            );

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'fk_agent_communication_logs_devices_device_id'
                ) THEN
                    ALTER TABLE intellinode.agent_communication_logs
                        ADD CONSTRAINT fk_agent_communication_logs_devices_device_id
                        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE SET NULL;
                END IF;
            END $$;

            CREATE INDEX IF NOT EXISTS ix_agent_communication_logs_device_id_created_utc
                ON intellinode.agent_communication_logs (device_id, created_utc DESC);

            INSERT INTO intellinode.discover_lookup (
                tenant_id, device_id, mac_address, host_name, ip_address, domain,
                os_name, os_version, agent_version, discovery_type, status,
                discovered_utc, updated_utc, approved_utc
            )
            SELECT
                d.tenant_id,
                d.id,
                d.mac_address,
                COALESCE(d.host_name, ''),
                COALESCE(d.ip_address, ''),
                COALESCE(d.domain, ''),
                COALESCE(d.os, ''),
                COALESCE(d.os_version, ''),
                COALESCE(d.agent_version, ''),
                'LegacyActive',
                'Approved'::intellinode.discover_lookup_status,
                d.created_utc,
                NOW(),
                d.created_utc
            FROM intellinode.devices d
            WHERE d.enrollment_state = 'Active'
              AND EXISTS (
                  SELECT 1 FROM intellinode.device_inventory di WHERE di.device_id = d.id
              )
              AND NOT EXISTS (
                  SELECT 1 FROM intellinode.discover_lookup dl
                  WHERE dl.tenant_id = d.tenant_id AND dl.mac_address = d.mac_address
              )
            ON CONFLICT (tenant_id, mac_address) DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM intellinode.discover_lookup
             WHERE discovery_type = 'LegacyActive';

            DROP TABLE IF EXISTS intellinode.agent_communication_logs;
            """);
    }
}
