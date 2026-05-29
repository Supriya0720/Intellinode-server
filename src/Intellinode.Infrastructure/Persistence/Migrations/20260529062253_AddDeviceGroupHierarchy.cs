using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeviceGroupHierarchy : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX IF EXISTS intellinode.ix_device_groups_tenant_id_name;
            DROP INDEX IF EXISTS intellinode.ix_device_groups_tenant_id_name_root;
            ALTER TABLE intellinode.device_groups
                DROP CONSTRAINT IF EXISTS device_groups_tenant_id_name_key;

            ALTER TABLE intellinode.device_groups
                ADD COLUMN IF NOT EXISTS parent_group_id uuid;
            ALTER TABLE intellinode.device_groups
                ADD COLUMN IF NOT EXISTS sort_order integer NOT NULL DEFAULT 0;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'fk_device_groups_device_groups_parent_group_id'
                ) THEN
                    ALTER TABLE intellinode.device_groups
                        ADD CONSTRAINT fk_device_groups_device_groups_parent_group_id
                        FOREIGN KEY (parent_group_id) REFERENCES intellinode.device_groups (id);
                END IF;
            END $$;

            CREATE INDEX IF NOT EXISTS ix_device_groups_parent_group_id
                ON intellinode.device_groups (parent_group_id);

            CREATE UNIQUE INDEX IF NOT EXISTS ix_device_groups_tenant_id_name
                ON intellinode.device_groups (tenant_id, name)
                WHERE parent_group_id IS NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS ix_device_groups_tenant_id_parent_group_id_name
                ON intellinode.device_groups (tenant_id, parent_group_id, name)
                WHERE parent_group_id IS NOT NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE intellinode.device_groups
                DROP CONSTRAINT IF EXISTS fk_device_groups_device_groups_parent_group_id;

            DROP INDEX IF EXISTS intellinode.ix_device_groups_parent_group_id;
            DROP INDEX IF EXISTS intellinode.ix_device_groups_tenant_id_name;
            DROP INDEX IF EXISTS intellinode.ix_device_groups_tenant_id_parent_group_id_name;

            ALTER TABLE intellinode.device_groups
                DROP COLUMN IF EXISTS parent_group_id;
            ALTER TABLE intellinode.device_groups
                DROP COLUMN IF EXISTS sort_order;

            CREATE UNIQUE INDEX IF NOT EXISTS ix_device_groups_tenant_id_name
                ON intellinode.device_groups (tenant_id, name);
            """);
    }
}
