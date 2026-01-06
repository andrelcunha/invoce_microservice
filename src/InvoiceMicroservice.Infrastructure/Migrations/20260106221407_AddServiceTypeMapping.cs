using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceTypeMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_type_tax_mappings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    service_type_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cnae_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nbs_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    service_list_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    operation_indicator = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_situation_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    tax_classification_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_type_tax_mappings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_type_tax_mappings_cnae_code",
                table: "service_type_tax_mappings",
                column: "cnae_code");

            migrationBuilder.CreateIndex(
                name: "IX_service_type_tax_mappings_is_active_service_type_key",
                table: "service_type_tax_mappings",
                columns: new[] { "is_active", "service_type_key" });

            migrationBuilder.CreateIndex(
                name: "IX_service_type_tax_mappings_service_type_key",
                table: "service_type_tax_mappings",
                column: "service_type_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_type_tax_mappings");
        }
    }
}
