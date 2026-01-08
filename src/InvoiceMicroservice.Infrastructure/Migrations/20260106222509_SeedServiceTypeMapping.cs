using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedServiceTypeMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "service_type_tax_mappings",
                columns:
                [
                    "service_type_key",
                    "cnae_code",
                    "description",
                    "nbs_code",
                    "service_list_code",
                    "operation_indicator",
                    "tax_situation_code",
                    "tax_classification_code",
                    "is_active",
                    "created_at"
                ],
                values: new object[,]
                {
                    {
                        "vehicle-wash-45200-05",
                        "45.20-0-05",
                        "Serviços de lavagem, lubrificação e polimento de veículos automotivos",
                        "1.1803.29.00",
                        "14.01",
                        "050101",
                        "200",
                        "000001",
                        true,
                        new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc)
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "service_type_tax_mappings",
                keyColumn: "service_type_key",
                keyValue: "vehicle-wash-45200-05");

        }
    }
}
