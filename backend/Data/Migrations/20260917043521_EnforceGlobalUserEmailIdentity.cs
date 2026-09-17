using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnforceGlobalUserEmailIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Users"
                        GROUP BY UPPER(BTRIM("Email", ' '))
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Cannot enforce global user email identity because duplicate email identities exist.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Users_CompanyId_Email",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Users"
                SET "NormalizedEmail" = UPPER(BTRIM("Email", ' '));
                """);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CompanyId_Email",
                table: "Users",
                columns: new[] { "CompanyId", "Email" },
                unique: true);
        }
    }
}
