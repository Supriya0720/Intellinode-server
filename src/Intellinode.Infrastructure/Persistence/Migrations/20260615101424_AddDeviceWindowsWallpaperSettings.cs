using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceWindowsWallpaperSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .Annotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
                .Annotation("Npgsql:Enum:discover_lookup_status.intellinode", "Pending,Approved,Rejected")
                .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled,PendingApproval,Rejected")
                .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .Annotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
                .Annotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
                .Annotation("Npgsql:Enum:intellinode.discover_lookup_status", "Approved,Pending,Rejected")
                .Annotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingApproval,PendingInventory,Rejected,Unlicensed")
                .Annotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
                .Annotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
                .Annotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,Display,General,Keyboard,Mouse,Windows8021x,WindowsComputerName,WindowsDateTimeSetup,WindowsEthernetSetup,WindowsPowerManagement,WindowsRegionLocation,WindowsRegionalFormat,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface,WindowsWallpaper,WindowsWirelessProperties,WindowsWirelessSetup")
                .Annotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
                .Annotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced,Keyboard,Mouse,Display,Windows8021x,WindowsComputerName,WindowsEthernetSetup,WindowsWirelessSetup,WindowsWirelessProperties,WindowsDateTimeSetup,WindowsRegionLocation,WindowsRegionalFormat,WindowsPowerManagement,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface")
                .OldAnnotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .OldAnnotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
                .OldAnnotation("Npgsql:Enum:discover_lookup_status.intellinode", "Pending,Approved,Rejected")
                .OldAnnotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled,PendingApproval,Rejected")
                .OldAnnotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .OldAnnotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
                .OldAnnotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
                .OldAnnotation("Npgsql:Enum:intellinode.discover_lookup_status", "Approved,Pending,Rejected")
                .OldAnnotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingApproval,PendingInventory,Rejected,Unlicensed")
                .OldAnnotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
                .OldAnnotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
                .OldAnnotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,Display,General,Keyboard,Mouse,Windows8021x,WindowsComputerName,WindowsDateTimeSetup,WindowsEthernetSetup,WindowsPowerManagement,WindowsRegionLocation,WindowsRegionalFormat,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface,WindowsWirelessProperties,WindowsWirelessSetup")
                .OldAnnotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
                .OldAnnotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced,Keyboard,Mouse,Display,Windows8021x,WindowsComputerName,WindowsEthernetSetup,WindowsWirelessSetup,WindowsWirelessProperties,WindowsDateTimeSetup,WindowsRegionLocation,WindowsRegionalFormat,WindowsPowerManagement,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface");

            migrationBuilder.AlterColumn<int>(
                name: "timeout_minutes",
                schema: "intellinode",
                table: "device_windows_screen_saver_settings_snapshots",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "timeout_minutes",
                schema: "intellinode",
                table: "device_windows_screen_saver_settings",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsWallpaper';
                """);

            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS intellinode.device_windows_wallpaper_settings (
                    device_id uuid NOT NULL,
                    source_type character varying(32) NOT NULL DEFAULT 'Browse',
                    picture_path character varying(512) NOT NULL,
                    picture_name character varying(256) NOT NULL,
                    picture_position character varying(32) NOT NULL,
                    prevent_user_changes boolean NOT NULL,
                    upload boolean NOT NULL,
                    agent_action integer NOT NULL DEFAULT 0,
                    repository_json jsonb NULL,
                    settings_version bigint NOT NULL DEFAULT 1,
                    pending_apply boolean NOT NULL DEFAULT false,
                    last_applied_version bigint NULL,
                    last_applied_utc timestamp with time zone NULL,
                    last_apply_status character varying(32) NULL,
                    last_apply_message character varying(500) NULL,
                    updated_by uuid NULL,
                    created_utc timestamp with time zone NOT NULL,
                    updated_utc timestamp with time zone NOT NULL,
                    CONSTRAINT pk_device_windows_wallpaper_settings PRIMARY KEY (device_id),
                    CONSTRAINT ck_device_windows_wallpaper_settings_settings_version CHECK (settings_version >= 0),
                    CONSTRAINT fk_device_windows_wallpaper_settings_devices_device_id
                        FOREIGN KEY (device_id) REFERENCES intellinode.devices (id) ON DELETE CASCADE
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_windows_wallpaper_settings",
                schema: "intellinode");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .Annotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
                .Annotation("Npgsql:Enum:discover_lookup_status.intellinode", "Pending,Approved,Rejected")
                .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled,PendingApproval,Rejected")
                .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .Annotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
                .Annotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
                .Annotation("Npgsql:Enum:intellinode.discover_lookup_status", "Approved,Pending,Rejected")
                .Annotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingApproval,PendingInventory,Rejected,Unlicensed")
                .Annotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
                .Annotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
                .Annotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,Display,General,Keyboard,Mouse,Windows8021x,WindowsComputerName,WindowsDateTimeSetup,WindowsEthernetSetup,WindowsPowerManagement,WindowsRegionLocation,WindowsRegionalFormat,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface,WindowsWirelessProperties,WindowsWirelessSetup")
                .Annotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
                .Annotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced,Keyboard,Mouse,Display,Windows8021x,WindowsComputerName,WindowsEthernetSetup,WindowsWirelessSetup,WindowsWirelessProperties,WindowsDateTimeSetup,WindowsRegionLocation,WindowsRegionalFormat,WindowsPowerManagement,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface")
                .OldAnnotation("Npgsql:Enum:agent_platform.intellinode", "Windows,Linux")
                .OldAnnotation("Npgsql:Enum:communication_type.intellinode", "HTTP,HTTPS,TCP")
                .OldAnnotation("Npgsql:Enum:discover_lookup_status.intellinode", "Pending,Approved,Rejected")
                .OldAnnotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled,PendingApproval,Rejected")
                .OldAnnotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName")
                .OldAnnotation("Npgsql:Enum:intellinode.agent_platform", "Linux,Windows")
                .OldAnnotation("Npgsql:Enum:intellinode.communication_type", "HTTP,HTTPS,TCP")
                .OldAnnotation("Npgsql:Enum:intellinode.discover_lookup_status", "Approved,Pending,Rejected")
                .OldAnnotation("Npgsql:Enum:intellinode.enrollment_state", "Active,Disabled,PendingApproval,PendingInventory,Rejected,Unlicensed")
                .OldAnnotation("Npgsql:Enum:intellinode.heartbeat_binding_kind", "HostName,IpAddress")
                .OldAnnotation("Npgsql:Enum:intellinode.settings_apply_status", "Applied,Delivered,Failed,Pending")
                .OldAnnotation("Npgsql:Enum:intellinode.settings_kind", "Advanced,Display,General,Keyboard,Mouse,Windows8021x,WindowsComputerName,WindowsDateTimeSetup,WindowsEthernetSetup,WindowsPowerManagement,WindowsRegionLocation,WindowsRegionalFormat,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface,WindowsWallpaper,WindowsWirelessProperties,WindowsWirelessSetup")
                .OldAnnotation("Npgsql:Enum:settings_apply_status.intellinode", "Pending,Delivered,Applied,Failed")
                .OldAnnotation("Npgsql:Enum:settings_kind.intellinode", "General,Advanced,Keyboard,Mouse,Display,Windows8021x,WindowsComputerName,WindowsEthernetSetup,WindowsWirelessSetup,WindowsWirelessProperties,WindowsDateTimeSetup,WindowsRegionLocation,WindowsRegionalFormat,WindowsPowerManagement,WindowsScreenSaver,WindowsTaskbar,WindowsUserInterface");

            migrationBuilder.AlterColumn<int>(
                name: "timeout_minutes",
                schema: "intellinode",
                table: "device_windows_screen_saver_settings_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "timeout_minutes",
                schema: "intellinode",
                table: "device_windows_screen_saver_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
