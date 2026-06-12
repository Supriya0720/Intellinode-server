using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: windows_power_plan_master, windows_power_timeout_master, device_windows_power_management_settings,
    /// settings_kind.WindowsPowerManagement. Down does not remove PostgreSQL enum values.
    /// </summary>
    public partial class AddWindowsPowerManagementReferenceAndDeviceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsPowerManagement';
                """);

            migrationBuilder.CreateTable(
                name: "device_windows_power_management_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    active_plan_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Balanced"),
                    agent_action = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
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
                    table.PrimaryKey("pk_device_windows_power_management_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_windows_power_management_settings_settings_version", "settings_version >= 0");
                    table.ForeignKey(
                        name: "fk_device_windows_power_management_settings_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "windows_power_plan_master",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    plan_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_windows_power_plan_master", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "windows_power_timeout_master",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    display_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_seconds = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_windows_power_timeout_master", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_windows_power_plan_master_is_active",
                schema: "intellinode",
                table: "windows_power_plan_master",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_windows_power_plan_master_plan_name",
                schema: "intellinode",
                table: "windows_power_plan_master",
                column: "plan_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_windows_power_timeout_master_category_is_active",
                schema: "intellinode",
                table: "windows_power_timeout_master",
                columns: new[] { "category", "is_active" });

            migrationBuilder.Sql(PowerManagementReferenceMasterSeedSql.PowerPlanSeed);
            migrationBuilder.Sql(PowerManagementReferenceMasterSeedSql.TimeoutSeed);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_windows_power_management_settings",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "windows_power_plan_master",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "windows_power_timeout_master",
                schema: "intellinode");
        }
    }
}
