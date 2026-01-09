using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExtinguishedAtFromMunicipality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "extinguished_at",
                table: "municipalities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "extinguished_at",
                table: "municipalities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}
