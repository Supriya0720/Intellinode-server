using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceWindowsRegionalFormatSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_windows_regional_format_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    time_separator = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    am_symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    pm_symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    short_date_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    date_separator = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    long_date_format = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    short_date_sample = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: ""),
                    long_date_sample = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: ""),
                    time_sample = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("pk_device_windows_regional_format_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_regional_format_settings_settings_version", "settings_version >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_regional_format_settings_devices_device_id",
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
                name: "device_windows_regional_format_settings",
                schema: "intellinode");
        }
    }
}
