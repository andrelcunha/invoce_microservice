using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPortalCredentialsWithMunicipalityRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "portal_type",
                table: "municipalities");

            migrationBuilder.AddColumn<int>(
                name: "municipality_id",
                table: "portal_credentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "portal_type",
                table: "portal_credentials",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_portal_credentials_municipality_id",
                table: "portal_credentials",
                column: "municipality_id");

            migrationBuilder.CreateIndex(
                name: "IX_portal_credentials_portal_type",
                table: "portal_credentials",
                column: "portal_type");

            migrationBuilder.AddForeignKey(
                name: "FK_portal_credentials_municipalities_municipality_id",
                table: "portal_credentials",
                column: "municipality_id",
                principalTable: "municipalities",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_portal_credentials_municipalities_municipality_id",
                table: "portal_credentials");

            migrationBuilder.DropIndex(
                name: "IX_portal_credentials_municipality_id",
                table: "portal_credentials");

            migrationBuilder.DropIndex(
                name: "IX_portal_credentials_portal_type",
                table: "portal_credentials");

            migrationBuilder.DropColumn(
                name: "municipality_id",
                table: "portal_credentials");

            migrationBuilder.DropColumn(
                name: "portal_type",
                table: "portal_credentials");

            migrationBuilder.AddColumn<string>(
                name: "portal_type",
                table: "municipalities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
