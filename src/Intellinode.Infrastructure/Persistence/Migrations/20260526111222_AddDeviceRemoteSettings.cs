using System;
using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 1 remote settings: adds 2 tables + 1 enum; no changes to devices table.
    /// Maps FusionX Remote_Client_Settings → device_remote_settings;
    /// appsettings AgentServer defaults → tenant_agent_defaults (seeded at startup).
    /// </summary>
    public partial class AddDeviceRemoteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .Annotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
                .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
                .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .Annotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
                .Annotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
                .Annotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingInventory,Unlicensed")
                .Annotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
                .OldAnnotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .OldAnnotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
                .OldAnnotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName");

            migrationBuilder.CreateTable(
                name: "tenant_agent_defaults",
                schema: "intellinode",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    api_base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    default_poll_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    default_communication_type = table.Column<CommunicationType>(type: "intellinode.communication_type", nullable: false, defaultValue: CommunicationType.HTTPS),
                    min_poll_interval_http = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_agent_defaults", x => x.tenant_id);
                    table.ForeignKey(
                        name: "fk_tenant_agent_defaults_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "intellinode",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_remote_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    server_port = table.Column<int>(type: "integer", nullable: false, defaultValue: 443),
                    poll_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    communication_type = table.Column<CommunicationType>(type: "intellinode.communication_type", nullable: false, defaultValue: CommunicationType.HTTPS),
                    agent_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    desired_group_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    agent_host_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    use_dhcp_discovery = table.Column<bool>(type: "boolean", nullable: false),
                    apply_on_reboot = table.Column<bool>(type: "boolean", nullable: false),
                    settings_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    pending_apply = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_remote_settings", x => x.device_id);
                    table.CheckConstraint(
                        "ck_device_remote_settings_poll_interval_seconds",
                        "poll_interval_seconds >= 1");
                    table.ForeignKey(
                        name: "fk_device_remote_settings_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_remote_settings",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "tenant_agent_defaults",
                schema: "intellinode");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
                .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .OldAnnotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .OldAnnotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
                .OldAnnotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
                .OldAnnotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .OldAnnotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
                .OldAnnotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
                .OldAnnotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingInventory,Unlicensed")
                .OldAnnotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress");
        }
    }
}
