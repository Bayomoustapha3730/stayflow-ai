using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guests_CompanyId_PhoneNumber",
                table: "Guests");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Guests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                table: "Guests",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "KE");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Guests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Guests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                table: "Guests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Guests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                table: "Guests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Guests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Guests",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.Sql("""
                UPDATE "Guests"
                SET
                    "FirstName" = LEFT(split_part(trim("FullName"), ' ', 1), 100),
                    "LastName" = LEFT(
                        CASE
                            WHEN position(' ' in trim("FullName")) > 0
                                THEN substring(trim("FullName") from position(' ' in trim("FullName")) + 1)
                            ELSE 'Unknown'
                        END,
                        100),
                    "CountryCode" = 'KE',
                    "PreferredLanguage" = 'en'
                """);

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Guests");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_CompanyId_Email",
                table: "Guests",
                columns: new[] { "CompanyId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Guests_CompanyId_PhoneNumber",
                table: "Guests",
                columns: new[] { "CompanyId", "PhoneNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Guests_Email",
                table: "Guests",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Guests_IsDeleted",
                table: "Guests",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guests_CompanyId_Email",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_CompanyId_PhoneNumber",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_Email",
                table: "Guests");

            migrationBuilder.DropIndex(
                name: "IX_Guests_IsDeleted",
                table: "Guests");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Guests",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Guests"
                SET "FullName" = LEFT(trim("FirstName" || ' ' || "LastName"), 160)
                """);

            migrationBuilder.DropColumn(
                name: "CountryCode",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Guests");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Guests");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Guests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guests_CompanyId_PhoneNumber",
                table: "Guests",
                columns: new[] { "CompanyId", "PhoneNumber" },
                unique: true);
        }
    }
}
