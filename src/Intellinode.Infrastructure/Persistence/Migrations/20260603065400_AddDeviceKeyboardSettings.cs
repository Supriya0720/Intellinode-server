using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: device_keyboard_settings, apply-log task linkage, settings_kind.Keyboard.
    /// Down does not remove the PostgreSQL enum value (not supported without recreating the type).
    /// </summary>
    public partial class AddDeviceKeyboardSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'Keyboard';
                """);

            migrationBuilder.AddColumn<int>(
                name: "legacy_task_id",
                schema: "intellinode",
                table: "device_settings_apply_log",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "task_id",
                schema: "intellinode",
                table: "device_settings_apply_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_keyboard_settings",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delay = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    repeat_rate = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    keyboard_locale = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    replace_existing_keyboard = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_device_keyboard_settings", x => x.device_id);
                    table.CheckConstraint("ck_device_keyboard_settings_delay", "delay >= 0");
                    table.CheckConstraint("ck_device_keyboard_settings_repeat_rate", "repeat_rate >= 0");
                    table.ForeignKey(
                        name: "fk_device_keyboard_settings_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_settings_apply_log_task_id",
                schema: "intellinode",
                table: "device_settings_apply_log",
                column: "task_id");

            migrationBuilder.AddForeignKey(
                name: "fk_device_settings_apply_log_device_tasks_task_id",
                schema: "intellinode",
                table: "device_settings_apply_log",
                column: "task_id",
                principalSchema: "intellinode",
                principalTable: "device_tasks",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_settings_apply_log_device_tasks_task_id",
                schema: "intellinode",
                table: "device_settings_apply_log");

            migrationBuilder.DropTable(
                name: "device_keyboard_settings",
                schema: "intellinode");

            migrationBuilder.DropIndex(
                name: "ix_device_settings_apply_log_task_id",
                schema: "intellinode",
                table: "device_settings_apply_log");

            migrationBuilder.DropColumn(
                name: "legacy_task_id",
                schema: "intellinode",
                table: "device_settings_apply_log");

            migrationBuilder.DropColumn(
                name: "task_id",
                schema: "intellinode",
                table: "device_settings_apply_log");
        }
    }
}
