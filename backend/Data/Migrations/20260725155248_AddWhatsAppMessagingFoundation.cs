using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppMessagingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_CompanyId_ExternalMessageId",
                table: "ConversationMessages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "ConversationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "ConversationMessages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedAt",
                table: "ConversationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "ConversationMessages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "ConversationMessages",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "ConversationMessages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReadAt",
                table: "ConversationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PhoneNumberId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WhatsAppBusinessAccountId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BusinessPhoneNumberMasked = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppIntegrations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId_Provider_ExternalMessageId",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "Provider", "ExternalMessageId" },
                unique: true,
                filter: "\"ExternalMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppIntegrations_CompanyId",
                table: "WhatsAppIntegrations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppIntegrations_CompanyId_IsActive",
                table: "WhatsAppIntegrations",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppIntegrations_IsActive",
                table: "WhatsAppIntegrations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppIntegrations_PhoneNumberId",
                table: "WhatsAppIntegrations",
                column: "PhoneNumberId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppIntegrations");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_CompanyId_Provider_ExternalMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "FailedAt",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "ConversationMessages");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId_ExternalMessageId",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "ExternalMessageId" },
                unique: true,
                filter: "\"ExternalMessageId\" IS NOT NULL");
        }
    }
}
