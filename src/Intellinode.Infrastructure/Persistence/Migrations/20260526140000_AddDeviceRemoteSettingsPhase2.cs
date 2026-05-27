using System;
using Intellinode.Domain.Enums;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 2: adds 4 tables + alters device_remote_settings; no changes to devices table.
    /// </summary>
    public partial class AddDeviceRemoteSettingsPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                .OldAnnotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
                .OldAnnotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .OldAnnotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
                .OldAnnotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
                .OldAnnotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingInventory,Unlicensed")
                .OldAnnotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress");

            migrationBuilder.AddColumn<bool>(
                name: "inherit_from_group",
                schema: "intellinode",
                table: "device_remote_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<long>(
                name: "last_applied_version",
                schema: "intellinode",
                table: "device_remote_settings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_applied_utc",
                schema: "intellinode",
                table: "device_remote_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_agent_advanced_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debug_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    heartbeat_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    application_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    usb_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    application_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    boot_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    screensaver_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    yum_monitor_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    signalr_monitoring_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    connection_type = table.Column<CommunicationType>(type: "intellinode.communication_type", nullable: false, defaultValue: CommunicationType.HTTPS),
                    dhcp_poll_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    always_apply = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    apply_on_next_reboot = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    inherit_from_group = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    settings_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    pending_apply = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_applied_version = table.Column<long>(type: "bigint", nullable: true),
                    last_applied_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    extra_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_agent_advanced_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_agent_advanced_settings_heartbeat_interval", "heartbeat_interval_seconds >= 1");
                    table.CheckConstraint("ck_device_agent_advanced_settings_application_interval", "application_interval_seconds >= 1");
                    table.CheckConstraint("ck_device_agent_advanced_settings_dhcp_poll_interval", "dhcp_poll_interval_seconds >= 1");
                    table.ForeignKey(
                        name: "fk_device_agent_advanced_settings_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_remote_settings",
                schema: "intellinode",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    server_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    server_port = table.Column<int>(type: "integer", nullable: false, defaultValue: 443),
                    poll_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    communication_type = table.Column<CommunicationType>(type: "intellinode.communication_type", nullable: false, defaultValue: CommunicationType.HTTPS),
                    agent_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    desired_group_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    agent_host_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    use_dhcp_discovery = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    apply_on_reboot = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    settings_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_remote_settings", x => x.group_id);
                    table.CheckConstraint("ck_group_remote_settings_poll_interval_seconds", "poll_interval_seconds >= 1");
                    table.ForeignKey(
                        name: "fk_group_remote_settings_device_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "intellinode",
                        principalTable: "device_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_agent_advanced_settings",
                schema: "intellinode",
                columns: table => new
                {
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    debug_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    heartbeat_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    application_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    usb_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    application_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    boot_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    screensaver_logs_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    yum_monitor_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    signalr_monitoring_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    connection_type = table.Column<CommunicationType>(type: "intellinode.communication_type", nullable: false, defaultValue: CommunicationType.HTTPS),
                    dhcp_poll_interval_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    always_apply = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    apply_on_next_reboot = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    settings_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_agent_advanced_settings", x => x.group_id);
                    table.CheckConstraint("ck_group_agent_advanced_settings_heartbeat_interval", "heartbeat_interval_seconds >= 1");
                    table.CheckConstraint("ck_group_agent_advanced_settings_application_interval", "application_interval_seconds >= 1");
                    table.CheckConstraint("ck_group_agent_advanced_settings_dhcp_poll_interval", "dhcp_poll_interval_seconds >= 1");
                    table.ForeignKey(
                        name: "fk_group_agent_advanced_settings_device_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "intellinode",
                        principalTable: "device_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_settings_apply_log",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settings_kind = table.Column<SettingsKind>(type: "intellinode.settings_kind", nullable: false),
                    settings_version = table.Column<long>(type: "bigint", nullable: false),
                    apply_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<SettingsApplyStatus>(type: "intellinode.settings_apply_status", nullable: false),
                    initiated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_settings_apply_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_settings_apply_log_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_settings_apply_log_device_id_created_utc",
                schema: "intellinode",
                table: "device_settings_apply_log",
                columns: new[] { "device_id", "created_utc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "device_settings_apply_log", schema: "intellinode");
            migrationBuilder.DropTable(name: "group_agent_advanced_settings", schema: "intellinode");
            migrationBuilder.DropTable(name: "group_remote_settings", schema: "intellinode");
            migrationBuilder.DropTable(name: "device_agent_advanced_settings", schema: "intellinode");

            migrationBuilder.DropColumn(name: "inherit_from_group", schema: "intellinode", table: "device_remote_settings");
            migrationBuilder.DropColumn(name: "last_applied_version", schema: "intellinode", table: "device_remote_settings");
            migrationBuilder.DropColumn(name: "last_applied_utc", schema: "intellinode", table: "device_remote_settings");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .Annotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
                .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
                .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
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
        }
    }
}
