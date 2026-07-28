using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppProductionTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialReference",
                table: "WhatsAppIntegrations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GraphApiVersion",
                table: "WhatsAppIntegrations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsProductionEnabled",
                table: "WhatsAppIntegrations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorSummary",
                table: "WhatsAppIntegrations",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastHealthCheckAt",
                table: "WhatsAppIntegrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSuccessfulHealthCheckAt",
                table: "WhatsAppIntegrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastTemplateSyncAt",
                table: "WhatsAppIntegrations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateSyncStatus",
                table: "WhatsAppIntegrations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WebhookConfigurationStatus",
                table: "WhatsAppIntegrations",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplateMessage",
                table: "ConversationMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateLanguageCode",
                table: "ConversationMessages",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "ConversationMessages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateRenderedPreview",
                table: "ConversationMessages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WhatsAppTemplateId",
                table: "ConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalTemplateId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HeaderType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    BodyText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    FooterText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VariableCount = table.Column<int>(type: "integer", nullable: false),
                    ComponentsJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WhatsAppTemplates_WhatsAppIntegrations_WhatsAppIntegrationId",
                        column: x => x.WhatsAppIntegrationId,
                        principalTable: "WhatsAppIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppIntegrations_CompanyId_IsProductionEnabled",
                table: "WhatsAppIntegrations",
                columns: new[] { "CompanyId", "IsProductionEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId_WhatsAppTemplateId",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "WhatsAppTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_CompanyId",
                table: "WhatsAppTemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_CompanyId_Status_IsActive",
                table: "WhatsAppTemplates",
                columns: new[] { "CompanyId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_IsActive",
                table: "WhatsAppTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppTemplates_WhatsAppIntegrationId_Name_LanguageCode",
                table: "WhatsAppTemplates",
                columns: new[] { "WhatsAppIntegrationId", "Name", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WhatsAppIntegrations_CompanyId_IsProductionEnabled",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_CompanyId_WhatsAppTemplateId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "CredentialReference",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "GraphApiVersion",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "IsProductionEnabled",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "LastErrorSummary",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "LastHealthCheckAt",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "LastSuccessfulHealthCheckAt",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "LastTemplateSyncAt",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "TemplateSyncStatus",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "WebhookConfigurationStatus",
                table: "WhatsAppIntegrations");

            migrationBuilder.DropColumn(
                name: "IsTemplateMessage",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "TemplateLanguageCode",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "TemplateRenderedPreview",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "WhatsAppTemplateId",
                table: "ConversationMessages");
        }
    }
}
