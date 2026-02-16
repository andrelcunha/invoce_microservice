using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceMicroservice.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveApiBaseUrlFromPortalCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {


            migrationBuilder.DropForeignKey(
                name: "FK_portal_credentials_municipalities_municipality_id",
                table: "portal_credentials");

            migrationBuilder.DropIndex(
                name: "IX_portal_credentials_issuer_cnpj",
                table: "portal_credentials");

            migrationBuilder.DropIndex(
                name: "IX_portal_credentials_municipality_id",
                table: "portal_credentials");

            migrationBuilder.DropIndex(
                name: "IX_portal_credentials_portal_type",
                table: "portal_credentials");

            migrationBuilder.DropColumn(
                name: "api_base_url",
                table: "portal_credentials");

            migrationBuilder.DropColumn(
                name: "issuer_cnpj",
                table: "portal_credentials");

            migrationBuilder.DropColumn(
                name: "municipality_id",
                table: "portal_credentials");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "portal_credentials",
                newName: "id");

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "portal_credentials",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<bool>(
                name: "requires_signature",
                table: "portal_credentials",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
                
            migrationBuilder.DropColumn(
                name: "portal_type",
                table: "portal_credentials");

            migrationBuilder.AddColumn<int>(
                name: "portal_type",
                table: "portal_credentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "portal_credentials",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "issuer_id",
                table: "portal_credentials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "issuers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    municipal_inscription = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cnae = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    address = table.Column<string>(type: "jsonb", nullable: false),
                    regime_tributario = table.Column<int>(type: "integer", nullable: false),
                    sub_regime_tributario = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now() at time zone 'utc'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issuers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_portal_credentials_issuer_id",
                table: "portal_credentials",
                column: "issuer_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_portal_credentials_issuer_id_portal_type_is_active",
                table: "portal_credentials",
                columns: new[] { "issuer_id", "portal_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_issuers_cnpj",
                table: "issuers",
                column: "cnpj",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_portal_credentials_issuers_issuer_id",
                table: "portal_credentials",
                column: "issuer_id",
                principalTable: "issuers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_portal_credentials_issuers_issuer_id",
                table: "portal_credentials");

            migrationBuilder.DropTable(
                name: "issuers");

            migrationBuilder.DropIndex(
                name: "IX_portal_credentials_issuer_id",
                table: "portal_credentials");

            migrationBuilder.DropIndex(
                name: "IX_portal_credentials_issuer_id_portal_type_is_active",
                table: "portal_credentials");

            migrationBuilder.DropColumn(
                name: "issuer_id",
                table: "portal_credentials");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "portal_credentials",
                newName: "Id");

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "portal_credentials",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<bool>(
                name: "requires_signature",
                table: "portal_credentials",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "portal_type",
                table: "portal_credentials",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "portal_credentials",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldDefaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<string>(
                name: "api_base_url",
                table: "portal_credentials",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "issuer_cnpj",
                table: "portal_credentials",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "municipality_id",
                table: "portal_credentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_portal_credentials_issuer_cnpj",
                table: "portal_credentials",
                column: "issuer_cnpj",
                unique: true);

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
    }
}
