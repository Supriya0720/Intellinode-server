using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceWindowsWallpaperSettingsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS intellinode.device_windows_wallpaper_settings_snapshots (
                    device_id uuid NOT NULL,
                    settings_version bigint NOT NULL,
                    source_type character varying(32) NOT NULL DEFAULT 'Browse',
                    picture_path character varying(512) NOT NULL,
                    picture_name character varying(256) NOT NULL,
                    picture_position character varying(32) NOT NULL,
                    prevent_user_changes boolean NOT NULL,
                    upload boolean NOT NULL,
                    agent_action integer NOT NULL DEFAULT 0,
                    repository_json jsonb NULL,
                    created_utc timestamp with time zone NOT NULL,
                    CONSTRAINT pk_device_windows_wallpaper_settings_snapshots PRIMARY KEY (device_id, settings_version),
                    CONSTRAINT fk_device_windows_wallpaper_settings_snapshots_devices_device_
                        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_windows_wallpaper_settings_snapshots",
                schema: "intellinode");
        }
    }
}
