using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Herensuge.Modules.Access.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class ManifestHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "manifest_hash",
                schema: "access",
                table: "resource_servers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "manifest_hash",
                schema: "access",
                table: "resource_servers");
        }
    }
}
