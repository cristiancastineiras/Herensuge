using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herensuge.Modules.Access.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Relationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "relation",
                schema: "access",
                table: "permissions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "relation_mode",
                schema: "access",
                table: "permissions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "relationship_store",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    store_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relationship_store", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "relationship_store",
                schema: "access");

            migrationBuilder.DropColumn(
                name: "relation",
                schema: "access",
                table: "permissions");

            migrationBuilder.DropColumn(
                name: "relation_mode",
                schema: "access",
                table: "permissions");
        }
    }
}
