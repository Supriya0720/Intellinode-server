using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR6: Immutable per-version snapshots for Windows 802.1X hydration.
    /// </summary>
    public partial class AddDeviceWindows8021xSettingsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_windows_802_1x_settings_snapshots",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settings_version = table.Column<long>(type: "bigint", nullable: false),
                    settings_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "pk_device_windows_802_1x_settings_snapshots",
                        x => new { x.device_id, x.settings_version });
                    table.CheckConstraint(
                        "ck_device_windows_802_1x_settings_snapshots_settings_version",
                        "settings_version >= 1");
                    table.ForeignKey(
                        name: "fk_device_windows_802_1x_settings_snapshots_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_windows_802_1x_settings_snapshots_device_version",
                schema: "intellinode",
                table: "device_windows_802_1x_settings_snapshots",
                columns: new[] { "device_id", "settings_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_windows_802_1x_settings_snapshots",
                schema: "intellinode");
        }
    }
}
