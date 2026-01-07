using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTypeMappingToInvoceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceTypeKey",
                table: "Invoices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceTypeKey",
                table: "Invoices");
        }
    }
}
