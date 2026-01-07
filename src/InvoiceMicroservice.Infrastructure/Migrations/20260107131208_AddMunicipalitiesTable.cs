using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMunicipalitiesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "municipalities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ibge_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    tom_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    extinguished_at = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_municipalities", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_municipalities_ibge_code",
                table: "municipalities",
                column: "ibge_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_municipalities_name_uf",
                table: "municipalities",
                columns: new[] { "name", "uf" });

            migrationBuilder.CreateIndex(
                name: "IX_municipalities_tom_code",
                table: "municipalities",
                column: "tom_code");

            migrationBuilder.CreateIndex(
                name: "IX_municipalities_uf",
                table: "municipalities",
                column: "uf");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "municipalities");
        }
    }
}
