using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: device_windows_screen_saver_settings, settings_kind.WindowsScreenSaver.
    /// Down does not remove the PostgreSQL enum value (not supported without recreating the type).
    /// </summary>
    public partial class AddDeviceWindowsScreenSaverSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsScreenSaver';
                """);

            migrationBuilder.CreateTable(
                name: "device_windows_screen_saver_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    screen_saver_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    timeout_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    password_protected = table.Column<bool>(type: "boolean", nullable: false),
                    prevent_user_changes = table.Column<bool>(type: "boolean", nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Browse"),
                    upload = table.Column<bool>(type: "boolean", nullable: false),
                    agent_action = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    repository_json = table.Column<string>(type: "jsonb", nullable: true),
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
                    table.PrimaryKey("pk_device_windows_screen_saver_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_screen_saver_settings_settings_version", "settings_version >= 0");
                    table.CheckConstraint("ck_device_windows_screen_saver_settings_timeout_minutes", "timeout_minutes >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_screen_saver_settings_devices_device_id",
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
                name: "device_windows_screen_saver_settings",
                schema: "intellinode");
        }
    }
}
