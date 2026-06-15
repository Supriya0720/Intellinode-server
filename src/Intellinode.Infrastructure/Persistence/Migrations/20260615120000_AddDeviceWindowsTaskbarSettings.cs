using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: device_windows_taskbar_settings, settings_kind.WindowsTaskbar.
    /// Down does not remove the PostgreSQL enum value (not supported without recreating the type).
    /// </summary>
    public partial class AddDeviceWindowsTaskbarSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsTaskbar';
                """);

            migrationBuilder.CreateTable(
                name: "device_windows_taskbar_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lock_taskbar = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    auto_hide_taskbar = table.Column<bool>(type: "boolean", nullable: false),
                    keep_taskbar_on_top = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    group_similar_buttons = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    show_quick_launch = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_device_windows_taskbar_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_taskbar_settings_settings_version", "settings_version >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_taskbar_settings_devices_device_id",
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
                name: "device_windows_taskbar_settings",
                schema: "intellinode");
        }
    }
}
