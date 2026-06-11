using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: device_windows_wireless_profile_settings + snapshots, settings_kind.WindowsWirelessProperties.
    /// Down does not remove the PostgreSQL enum value (not supported without recreating the type).
    /// </summary>
    public partial class AddDeviceWindowsWirelessProfileSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsWirelessProperties';
                """);

            migrationBuilder.CreateTable(
                name: "device_windows_wireless_profile_settings",
                schema: "intellinode",
                columns: table => new
                {
                    profile_key = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ssid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    settings_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
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
                    table.PrimaryKey("pk_device_windows_wireless_profile_settings", x => x.profile_key);
                    table.CheckConstraint("ck_device_windows_wireless_profile_settings_settings_version", "settings_version >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_wireless_profile_settings_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_windows_wireless_profile_settings_snapshots",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_key = table.Column<long>(type: "bigint", nullable: false),
                    settings_version = table.Column<long>(type: "bigint", nullable: false),
                    settings_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_windows_wireless_profile_settings_snapshots", x => new { x.device_id, x.profile_key, x.settings_version });
                    table.CheckConstraint("ck_device_windows_wireless_profile_settings_snapshots_settings~", "settings_version >= 1");
                    table.ForeignKey(
                        name: "fk_device_windows_wireless_profile_settings_snapshots_device_w",
                        column: x => x.profile_key,
                        principalSchema: "intellinode",
                        principalTable: "device_windows_wireless_profile_settings",
                        principalColumn: "profile_key",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_device_windows_wireless_profile_settings_snapshots_devices_",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_windows_wireless_profile_settings_device_ssid",
                schema: "intellinode",
                table: "device_windows_wireless_profile_settings",
                columns: new[] { "device_id", "ssid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_windows_wireless_profile_settings_snapshots_device_profile_version",
                schema: "intellinode",
                table: "device_windows_wireless_profile_settings_snapshots",
                columns: new[] { "device_id", "profile_key", "settings_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_windows_wireless_profile_settings_snapshots_profile_",
                schema: "intellinode",
                table: "device_windows_wireless_profile_settings_snapshots",
                column: "profile_key");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_windows_wireless_profile_settings_snapshots",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "device_windows_wireless_profile_settings",
                schema: "intellinode");
        }
    }
}
