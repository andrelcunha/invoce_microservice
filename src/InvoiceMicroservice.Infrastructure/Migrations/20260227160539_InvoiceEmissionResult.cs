using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceEmissionResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_emission_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuerCnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    PortalType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NumeroDfe = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SerieDfe = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CodStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StatusDescription = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Protocolo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ChaveAcesso = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    VerificationCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DocumentUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RequestXml = table.Column<string>(type: "text", nullable: true),
                    ResponseRaw = table.Column<string>(type: "text", nullable: true),
                    AlertsJson = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_emission_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoice_emission_results_invoice_emission_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "invoice_emission_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoice_emission_results_ChaveAcesso",
                table: "invoice_emission_results",
                column: "ChaveAcesso");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_emission_results_IssuerCnpj",
                table: "invoice_emission_results",
                column: "IssuerCnpj");

            migrationBuilder.CreateIndex(
                name: "IX_invoice_emission_results_JobId",
                table: "invoice_emission_results",
                column: "JobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_emission_results");
        }
    }
}
