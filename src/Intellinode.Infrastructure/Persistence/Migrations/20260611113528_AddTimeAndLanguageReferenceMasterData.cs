using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PR1: region_and_location_master, windows_time_zone_master, settings_kind time/language values.
    /// Time zone seed ported from FusionX UCWindowsDateTimeSettings.ascx ddlTimeZone (184 rows).
    /// Region seed is a representative FusionX subset; expand in later migrations as needed.
    /// Down does not remove PostgreSQL enum values (not supported without recreating the type).
    /// </summary>
    public partial class AddTimeAndLanguageReferenceMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsDateTimeSetup';
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsRegionLocation';
                ALTER TYPE intellinode.settings_kind ADD VALUE IF NOT EXISTS 'WindowsRegionalFormat';
                """);

            migrationBuilder.CreateTable(
                name: "region_and_location_master",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    identifier = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    bcp47code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_region_and_location_master", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "windows_time_zone_master",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    windows_tz_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_windows_time_zone_master", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_region_and_location_master_identifier_is_active",
                schema: "intellinode",
                table: "region_and_location_master",
                columns: new[] { "identifier", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_windows_time_zone_master_display_name",
                schema: "intellinode",
                table: "windows_time_zone_master",
                column: "display_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_windows_time_zone_master_is_active",
                schema: "intellinode",
                table: "windows_time_zone_master",
                column: "is_active");

            migrationBuilder.Sql(TimeAndLanguageReferenceMasterSeedSql.RegionSeed);
            migrationBuilder.Sql(TimeAndLanguageReferenceMasterSeedSql.TimeZoneSeed);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "region_and_location_master",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "windows_time_zone_master",
                schema: "intellinode");
        }
    }
}
