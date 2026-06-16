using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: device_windows_application_command_settings, settings_kind WindowsApplication + WindowsCommand.
    /// Down does not remove PostgreSQL enum values (not supported without recreating the type).
    /// </summary>
    public partial class AddDeviceWindowsApplicationCommandSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsApplication';
                """);

            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsCommand';
                """);

            migrationBuilder.CreateTable(
                name: "device_windows_application_command_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, defaultValue: "Application"),
                    application_path = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    parameters = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    warn_user = table.Column<bool>(type: "boolean", nullable: false),
                    alert_title = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    alert_message = table.Column<string>(type: "character varying(87)", maxLength: 87, nullable: false),
                    message_type = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    display_time = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    command_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    timeout = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    reboot_required = table.Column<bool>(type: "boolean", nullable: false),
                    require_command_output = table.Column<bool>(type: "boolean", nullable: false),
                    agent_action = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    settings_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    pending_apply = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    last_applied_version = table.Column<long>(type: "bigint", nullable: true),
                    last_applied_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_apply_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_apply_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_windows_application_command_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_application_command_settings_settings_version", "settings_version >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_application_command_settings_devices_device_id",
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
                name: "device_windows_application_command_settings",
                schema: "intellinode");
        }
    }
}
