using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveWhatsAppCloudApiSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RetryOfMessageId",
                table: "ConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SendAttemptNumber",
                table: "ConversationMessages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId_ConversationId_DeliveryStatus",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "ConversationId", "DeliveryStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_RetryOfMessageId",
                table: "ConversationMessages",
                column: "RetryOfMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationMessages_ConversationMessages_RetryOfMessageId",
                table: "ConversationMessages",
                column: "RetryOfMessageId",
                principalTable: "ConversationMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationMessages_ConversationMessages_RetryOfMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_CompanyId_ConversationId_DeliveryStatus",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_RetryOfMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "RetryOfMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "SendAttemptNumber",
                table: "ConversationMessages");
        }
    }
}
