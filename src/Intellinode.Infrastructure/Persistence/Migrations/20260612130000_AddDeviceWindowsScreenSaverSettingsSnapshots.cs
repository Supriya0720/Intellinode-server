using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR3: immutable snapshots for repository/upload hydration (ADR-0005 Option B).
    /// </summary>
    public partial class AddDeviceWindowsScreenSaverSettingsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_windows_screen_saver_settings_snapshots",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settings_version = table.Column<long>(type: "bigint", nullable: false),
                    screen_saver_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    timeout_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    password_protected = table.Column<bool>(type: "boolean", nullable: false),
                    prevent_user_changes = table.Column<bool>(type: "boolean", nullable: false),
                    source_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Browse"),
                    upload = table.Column<bool>(type: "boolean", nullable: false),
                    agent_action = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    repository_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_windows_screen_saver_settings_snapshots", x => new { x.device_id, x.settings_version });
                    table.CheckConstraint("ck_device_windows_screen_saver_settings_snapshots_timeout_minutes", "timeout_minutes >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_screen_saver_settings_snapshots_devices_devi",
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
                name: "device_windows_screen_saver_settings_snapshots",
                schema: "intellinode");
        }
    }
}
