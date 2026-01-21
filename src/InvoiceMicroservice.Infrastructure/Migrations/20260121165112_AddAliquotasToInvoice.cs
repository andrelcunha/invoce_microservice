using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAliquotasToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaCofins",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaPis",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "IbsCbsClassTrib",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IbsCbsCst",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MunicipalTaxCode",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PisCofinsCts",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoRetencaoPisCofins",
                table: "Invoices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AliquotaCofins",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "AliquotaPis",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IbsCbsClassTrib",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IbsCbsCst",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "MunicipalTaxCode",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PisCofinsCts",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TipoRetencaoPisCofins",
                table: "Invoices");
        }
    }
}
