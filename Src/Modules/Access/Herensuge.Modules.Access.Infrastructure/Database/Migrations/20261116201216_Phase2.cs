using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herensuge.Modules.Access.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Phase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "user_id",
                schema: "access",
                table: "role_assignments",
                newName: "subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_role_assignments_user_id",
                schema: "access",
                table: "role_assignments",
                newName: "ix_role_assignments_subject_id");

            migrationBuilder.RenameIndex(
                name: "ix_role_assignments_tenant_id_user_id",
                schema: "access",
                table: "role_assignments",
                newName: "ix_role_assignments_tenant_id_subject_id");

            migrationBuilder.AddColumn<string>(
                name: "subject_type",
                schema: "access",
                table: "role_assignments",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "user");

            migrationBuilder.AddColumn<string>(
                name: "feature_key",
                schema: "access",
                table: "permissions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "features",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_server_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_features", x => x.id);
                    table.ForeignKey(
                        name: "fk_features_resource_servers_resource_server_id",
                        column: x => x.resource_server_id,
                        principalSchema: "access",
                        principalTable: "resource_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    member_user_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    features = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_accounts",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_plans",
                schema: "access",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_plans", x => x.tenant_id);
                    table.ForeignKey(
                        name: "fk_tenant_plans_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "access",
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_features_resource_server_id_key",
                schema: "access",
                table: "features",
                columns: new[] { "resource_server_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_groups_member_user_ids",
                schema: "access",
                table: "groups",
                column: "member_user_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_groups_tenant_id",
                schema: "access",
                table: "groups",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_plans_key",
                schema: "access",
                table: "plans",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_accounts_client_id",
                schema: "access",
                table: "service_accounts",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_accounts_tenant_id",
                schema: "access",
                table: "service_accounts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_plans_plan_id",
                schema: "access",
                table: "tenant_plans",
                column: "plan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "features",
                schema: "access");

            migrationBuilder.DropTable(
                name: "groups",
                schema: "access");

            migrationBuilder.DropTable(
                name: "service_accounts",
                schema: "access");

            migrationBuilder.DropTable(
                name: "tenant_plans",
                schema: "access");

            migrationBuilder.DropTable(
                name: "plans",
                schema: "access");

            migrationBuilder.DropColumn(
                name: "subject_type",
                schema: "access",
                table: "role_assignments");

            migrationBuilder.DropColumn(
                name: "feature_key",
                schema: "access",
                table: "permissions");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                schema: "access",
                table: "role_assignments",
                newName: "user_id");

            migrationBuilder.RenameIndex(
                name: "ix_role_assignments_tenant_id_subject_id",
                schema: "access",
                table: "role_assignments",
                newName: "ix_role_assignments_tenant_id_user_id");

            migrationBuilder.RenameIndex(
                name: "ix_role_assignments_subject_id",
                schema: "access",
                table: "role_assignments",
                newName: "ix_role_assignments_user_id");
        }
    }
}
