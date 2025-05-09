using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GariusStorage.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixUserIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExternalUser",
                table: "AspNetRoles");

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalUser",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExternalUser",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalUser",
                table: "AspNetRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
