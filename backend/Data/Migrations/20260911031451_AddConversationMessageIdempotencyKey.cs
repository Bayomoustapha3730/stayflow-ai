using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationMessageIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "ConversationMessages",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_ConversationMessages_CompanyId_IdempotencyKey",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ConversationMessages_CompanyId_IdempotencyKey",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "ConversationMessages");
        }
    }
}
