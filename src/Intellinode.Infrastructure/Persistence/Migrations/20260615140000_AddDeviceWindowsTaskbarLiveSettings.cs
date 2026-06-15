using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR4: agent-reported live taskbar state (FusionX XPTaskbar_Details parity).
    /// </summary>
    public partial class AddDeviceWindowsTaskbarLiveSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_windows_taskbar_live_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lock_taskbar = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    auto_hide_taskbar = table.Column<bool>(type: "boolean", nullable: false),
                    keep_taskbar_on_top = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    group_similar_buttons = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    show_quick_launch = table.Column<bool>(type: "boolean", nullable: false),
                    show_clock = table.Column<bool>(type: "boolean", nullable: false),
                    hide_inactive_icons = table.Column<bool>(type: "boolean", nullable: false),
                    collected_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    report_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_windows_taskbar_live_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_taskbar_live_settings_report_version", "report_version >= 1");
                    table.ForeignKey(
                        name: "fk_device_windows_taskbar_live_settings_devices_device_id",
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
                name: "device_windows_taskbar_live_settings",
                schema: "intellinode");
        }
    }
}
