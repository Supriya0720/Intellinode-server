using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWindowsPowerAdvancedOptionMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "windows_power_advanced_option_master",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    plan_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    option_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    setting_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_windows_power_advanced_option_master", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_windows_power_advanced_option_master_option_name_is_active",
                schema: "intellinode",
                table: "windows_power_advanced_option_master",
                columns: new[] { "option_name", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_windows_power_advanced_option_master_plan_name_option_name_",
                schema: "intellinode",
                table: "windows_power_advanced_option_master",
                columns: new[] { "plan_name", "option_name", "setting_name", "is_active" });

            migrationBuilder.Sql(PowerManagementAdvancedOptionMasterSeedSql.AdvancedOptionSeed);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "windows_power_advanced_option_master",
                schema: "intellinode");
        }
    }
}
