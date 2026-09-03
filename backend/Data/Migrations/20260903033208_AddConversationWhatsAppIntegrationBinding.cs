using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationWhatsAppIntegrationBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WhatsAppIntegrationId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WhatsAppIntegrationId",
                table: "Conversations",
                column: "WhatsAppIntegrationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_WhatsAppIntegrations_WhatsAppIntegrationId",
                table: "Conversations",
                column: "WhatsAppIntegrationId",
                principalTable: "WhatsAppIntegrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_WhatsAppIntegrations_WhatsAppIntegrationId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_WhatsAppIntegrationId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "WhatsAppIntegrationId",
                table: "Conversations");
        }
    }
}
