using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropInvoicesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    AliquotaCofins = table.Column<decimal>(type: "numeric", nullable: false),
                    AliquotaPis = table.Column<decimal>(type: "numeric", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConsumerData = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    ErrorDetails = table.Column<string>(type: "jsonb", nullable: true),
                    ExternalInvoiceId = table.Column<string>(type: "text", nullable: true),
                    ExternalResponse = table.Column<string>(type: "jsonb", nullable: true),
                    IbsCbsClassTrib = table.Column<string>(type: "text", nullable: true),
                    IbsCbsCst = table.Column<string>(type: "text", nullable: true),
                    IssRate = table.Column<decimal>(type: "numeric", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuerCnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    IssuerData = table.Column<string>(type: "jsonb", nullable: false),
                    MunicipalTaxCode = table.Column<string>(type: "text", nullable: true),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    PisCofinsCts = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Series = table.Column<int>(type: "integer", nullable: false),
                    ServiceDescription = table.Column<string>(type: "text", nullable: false),
                    ServiceTypeKey = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TipoRetencaoPisCofins = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    XMLResponse = table.Column<string>(type: "text", nullable: true),
                    XmlPayload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ClientId",
                table: "Invoices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ExternalInvoiceId",
                table: "Invoices",
                column: "ExternalInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_IssuerCnpj",
                table: "Invoices",
                column: "IssuerCnpj");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_Status_CreatedAt",
                table: "Invoices",
                columns: new[] { "Status", "CreatedAt" });
        }
    }
}
