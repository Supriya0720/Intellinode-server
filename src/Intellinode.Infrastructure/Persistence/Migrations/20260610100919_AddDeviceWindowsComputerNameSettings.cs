using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: device_windows_computer_name_settings, settings_kind.WindowsComputerName.
    /// Down does not remove the PostgreSQL enum value (not supported without recreating the type).
    /// </summary>
    public partial class AddDeviceWindowsComputerNameSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsComputerName';
                """);

            migrationBuilder.CreateTable(
                name: "device_windows_computer_name_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    apply_mode = table.Column<int>(type: "integer", nullable: false),
                    host_name = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    domain = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    work_group = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    organizational_unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    user_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_domain_join = table.Column<bool>(type: "boolean", nullable: false),
                    prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    postfix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    no_of_char = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_mac_or_serial = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("pk_device_windows_computer_name_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_computer_name_settings_settings_version", "settings_version >= 0");
                    table.CheckConstraint("ck_device_windows_computer_name_settings_no_of_char", "no_of_char >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_computer_name_settings_devices_device_id",
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
                name: "device_windows_computer_name_settings",
                schema: "intellinode");
        }
    }
}
