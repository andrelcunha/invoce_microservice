using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIssRateToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IssRate",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssRate",
                table: "Invoices");
        }
    }
}
