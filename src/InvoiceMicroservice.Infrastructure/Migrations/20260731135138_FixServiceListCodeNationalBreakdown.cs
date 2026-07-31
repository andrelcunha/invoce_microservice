using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixServiceListCodeNationalBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "service_type_tax_mappings",
                keyColumn: "service_type_key",
                keyValue: "vehicle-wash-45200-05",
                column: "service_list_code",
                value: "1401.01");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "service_type_tax_mappings",
                keyColumn: "service_type_key",
                keyValue: "vehicle-wash-45200-05",
                column: "service_list_code",
                value: "14.01");
        }
    }
}
