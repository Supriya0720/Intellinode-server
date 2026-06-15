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
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS intellinode.device_windows_taskbar_live_settings (
                    device_id uuid NOT NULL,
                    lock_taskbar boolean NOT NULL DEFAULT true,
                    auto_hide_taskbar boolean NOT NULL,
                    keep_taskbar_on_top boolean NOT NULL DEFAULT true,
                    group_similar_buttons boolean NOT NULL DEFAULT true,
                    show_quick_launch boolean NOT NULL,
                    show_clock boolean NOT NULL,
                    hide_inactive_icons boolean NOT NULL,
                    collected_utc timestamp with time zone NOT NULL,
                    report_version bigint NOT NULL DEFAULT 1,
                    created_utc timestamp with time zone NOT NULL,
                    updated_utc timestamp with time zone NOT NULL,
                    CONSTRAINT pk_device_windows_taskbar_live_settings PRIMARY KEY (device_id),
                    CONSTRAINT ck_device_windows_taskbar_live_settings_report_version CHECK (report_version >= 1),
                    CONSTRAINT fk_device_windows_taskbar_live_settings_devices_device_id
                        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
                );
                """);
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
