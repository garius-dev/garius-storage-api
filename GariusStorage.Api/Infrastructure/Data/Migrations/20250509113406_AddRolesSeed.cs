using Microsoft.EntityFrameworkCore.Migrations;
using GariusStorage.Api.Domain.Constants;

#nullable disable

namespace GariusStorage.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles", // Nome da tabela de roles do Identity (geralmente AspNetRoles)
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" }, // Colunas a serem inseridas
                values: new object[,]
                {
                    {
                        RoleConstants.AdminRoleId, // Use o Guid constante
                        RoleConstants.AdminRoleName,
                        RoleConstants.AdminRoleName.ToUpperInvariant(), // Nome normalizado
                        Guid.NewGuid().ToString() // ConcurrencyStamp pode ser um novo Guid para cada seed
                    },
                    {
                        RoleConstants.OwnerRoleId,
                        RoleConstants.OwnerRoleName,
                        RoleConstants.OwnerRoleName.ToUpperInvariant(),
                        Guid.NewGuid().ToString()
                    },
                    {
                        RoleConstants.DeveloperRoleId,
                        RoleConstants.DeveloperRoleName,
                        RoleConstants.DeveloperRoleName.ToUpperInvariant(),
                        Guid.NewGuid().ToString()
                    },
                    {
                        RoleConstants.UserRoleId,
                        RoleConstants.UserRoleName,
                        RoleConstants.UserRoleName.ToUpperInvariant(),
                        Guid.NewGuid().ToString()
                    }
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValues: new object[] {
                    RoleConstants.AdminRoleId,
                    RoleConstants.OwnerRoleId,
                    RoleConstants.DeveloperRoleId,
                    RoleConstants.UserRoleId
                }
            );
        }
    }
}
