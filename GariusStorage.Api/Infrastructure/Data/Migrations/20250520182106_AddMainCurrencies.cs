using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GariusStorage.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMainCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            
            var brlId = Guid.Parse("7902b082-48d3-4924-8cf6-36872137a1bb"); // Exemplo de GUID fixo
            var usdId = Guid.Parse("dcbcd135-87ea-40e7-9100-caccc9e9462e"); // Exemplo de GUID fixo
            var eurId = Guid.Parse("64e21c18-1aec-4ab7-8852-d37f2c7c5bfc"); // Exemplo de GUID fixo
            var jpyId = Guid.Parse("3c9a4190-5837-4139-ae7b-14157c5d91e3"); // Exemplo de GUID fixo
            var cnyId = Guid.Parse("8ff5a8e7-bf8f-4df3-ab3c-a34ec4470702"); // Exemplo de GUID fixo
            var now = DateTime.UtcNow;

            migrationBuilder.InsertData(
                table: "Currencies",
                columns: new[] { "Id", "Code", "Name", "Symbol", "IsDefault", "CreatedAt", "LastUpdate", "Enabled" },
                values: new object[,]
                {
                    { brlId, "BRL", "Real Brasileiro", "R$", true, now, now, true },
                    { usdId, "USD", "Dólar Americano", "$", false, now, now, true },
                    { eurId, "EUR", "Euro", "€", false, now, now, true },
                    { jpyId, "JPY", "Iene Japonês", "¥", false, now, now, true },
                    { cnyId, "CNY", "Yuan Chinês", "¥", false, now, now, true }
                });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
           
            // Remover os dados das moedas inseridos
            // É importante usar as mesmas chaves (Id) que foram usadas no InsertData
            var brlId = Guid.Parse("7902b082-48d3-4924-8cf6-36872137a1bb"); // Exemplo de GUID fixo
            var usdId = Guid.Parse("dcbcd135-87ea-40e7-9100-caccc9e9462e"); // Exemplo de GUID fixo
            var eurId = Guid.Parse("64e21c18-1aec-4ab7-8852-d37f2c7c5bfc"); // Exemplo de GUID fixo
            var jpyId = Guid.Parse("3c9a4190-5837-4139-ae7b-14157c5d91e3"); // Exemplo de GUID fixo
            var cnyId = Guid.Parse("8ff5a8e7-bf8f-4df3-ab3c-a34ec4470702"); // Exemplo de GUID fixo

            migrationBuilder.DeleteData(
                table: "Currencies",
                keyColumn: "Id",
                keyValues: new object[] { brlId, usdId, eurId, jpyId, cnyId });
        }
    }
}
