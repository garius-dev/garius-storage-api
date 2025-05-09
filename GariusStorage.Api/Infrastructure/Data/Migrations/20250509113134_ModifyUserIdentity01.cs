using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GariusStorage.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModifyUserIdentity01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsExternalUser",
                table: "AspNetRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsExternalUser",
                table: "AspNetRoles");
        }
    }
}
