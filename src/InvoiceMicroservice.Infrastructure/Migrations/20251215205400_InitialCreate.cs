using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IssuerCnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    IssuerData = table.Column<string>(type: "jsonb", nullable: false),
                    ConsumerData = table.Column<string>(type: "jsonb", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    ServiceDescription = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExternalInvoiceId = table.Column<string>(type: "text", nullable: true),
                    XmlPayload = table.Column<string>(type: "text", nullable: true),
                    XMLResponse = table.Column<string>(type: "text", nullable: true),
                    ExternalResponse = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorDetails = table.Column<string>(type: "jsonb", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");
        }
    }
}
