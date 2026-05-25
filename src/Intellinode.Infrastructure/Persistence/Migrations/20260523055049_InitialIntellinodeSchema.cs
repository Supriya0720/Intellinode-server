using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Intellinode.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntellinodeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "intellinode");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:enrollment_state.intellinode", "PendingInventory,Active,Unlicensed,Disabled")
                .Annotation("Npgsql:Enum:heartbeat_binding_kind.intellinode", "IpAddress,HostName");

            migrationBuilder.CreateTable(
                name: "admin_users",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    host_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_groups",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_groups_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "intellinode",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mac_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    host_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    communication_ip_address = table.Column<string>(type: "text", nullable: false),
                    subnet_mask = table.Column<string>(type: "text", nullable: false),
                    gateway = table.Column<string>(type: "text", nullable: false),
                    primary_dns = table.Column<string>(type: "text", nullable: false),
                    secondary_dns = table.Column<string>(type: "text", nullable: false),
                    primary_wins = table.Column<string>(type: "text", nullable: false),
                    secondary_wins = table.Column<string>(type: "text", nullable: false),
                    domain = table.Column<string>(type: "text", nullable: false),
                    workgroup = table.Column<string>(type: "text", nullable: false),
                    login_user_name = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "text", nullable: false),
                    license_key = table.Column<string>(type: "text", nullable: false),
                    communication_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agent_up_time = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    duration = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    poll_interval = table.Column<int>(type: "integer", nullable: false),
                    is_dhcp = table.Column<bool>(type: "boolean", nullable: false),
                    is_domain_joined = table.Column<bool>(type: "boolean", nullable: false),
                    is_online = table.Column<bool>(type: "boolean", nullable: false),
                    is_service_mode = table.Column<bool>(type: "boolean", nullable: false),
                    is_licensed = table.Column<bool>(type: "boolean", nullable: false),
                    is_registered = table.Column<bool>(type: "boolean", nullable: false),
                    enrollment_state = table.Column<int>(type: "intellinode.enrollment_state", nullable: false),
                    client_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    os = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    os_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    agent_version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_heartbeat_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_devices_device_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "intellinode",
                        principalTable: "device_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_devices_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "intellinode",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_enrollment_tokens",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    mac_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_enrollment_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_enrollment_tokens_admin_users_created_by_admin_id",
                        column: x => x.created_by_admin_id,
                        principalSchema: "intellinode",
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_agent_enrollment_tokens_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agent_refresh_tokens",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agent_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_agent_refresh_tokens_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_inventory",
                schema: "intellinode",
                columns: table => new
                {
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hardware = table.Column<string>(type: "jsonb", nullable: true),
                    network = table.Column<string>(type: "jsonb", nullable: true),
                    os_info = table.Column<string>(type: "jsonb", nullable: true),
                    security = table.Column<string>(type: "jsonb", nullable: true),
                    collected_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_inventory", x => x.device_id);
                    table.ForeignKey(
                        name: "fk_device_inventory_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_tasks",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legacy_task_id = table.Column<int>(type: "integer", nullable: false),
                    module_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    function_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    function_parameter = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    extra_data = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_tasks_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "heartbeat_binding_changes",
                schema: "intellinode",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_service_mode = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    changed_value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    kind = table.Column<int>(type: "intellinode.heartbeat_binding_kind", nullable: false),
                    is_binding_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_heartbeat_binding_changes", x => x.id);
                    table.ForeignKey(
                        name: "fk_heartbeat_binding_changes_devices_device_id",
                        column: x => x.device_id,
                        principalSchema: "intellinode",
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_user_name",
                schema: "intellinode",
                table: "admin_users",
                column: "user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_enrollment_tokens_created_by_admin_id",
                schema: "intellinode",
                table: "agent_enrollment_tokens",
                column: "created_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_enrollment_tokens_device_id",
                schema: "intellinode",
                table: "agent_enrollment_tokens",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_enrollment_tokens_token_hash",
                schema: "intellinode",
                table: "agent_enrollment_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_agent_refresh_tokens_device_id",
                schema: "intellinode",
                table: "agent_refresh_tokens",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_refresh_tokens_token_hash",
                schema: "intellinode",
                table: "agent_refresh_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "ix_device_groups_tenant_id_name",
                schema: "intellinode",
                table: "device_groups",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_tasks_device_id",
                schema: "intellinode",
                table: "device_tasks",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_group_id",
                schema: "intellinode",
                table: "devices",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_devices_tenant_id_mac_address",
                schema: "intellinode",
                table: "devices",
                columns: new[] { "tenant_id", "mac_address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_heartbeat_binding_changes_device_id",
                schema: "intellinode",
                table: "heartbeat_binding_changes",
                column: "device_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_enrollment_tokens",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "agent_refresh_tokens",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "device_inventory",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "device_tasks",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "heartbeat_binding_changes",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "admin_users",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "devices",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "device_groups",
                schema: "intellinode");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "intellinode");
        }
    }
}
