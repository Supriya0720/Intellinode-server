using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR2: device_windows_date_time_settings. settings_kind.WindowsDateTimeSetup exists from PR1 — not re-added here.
    /// Down does not remove PostgreSQL enum values.
    /// </summary>
    public partial class AddDeviceWindowsDateTimeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_windows_date_time_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    apply_mode = table.Column<int>(type: "integer", nullable: false),
                    current_date_local = table.Column<DateOnly>(type: "date", nullable: true),
                    current_time_local = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    time_zone_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    windows_tz_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    time_server = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
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
                    table.PrimaryKey("pk_device_windows_date_time_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_date_time_settings_settings_version", "settings_version >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_date_time_settings_devices_device_id",
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
                name: "device_windows_date_time_settings",
                schema: "intellinode");
        }
    }
}
