using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationLifecycleWhatsAppTemplateMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationLifecycleWhatsAppTemplateMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatsAppTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    JourneyEventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ParameterBindings = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationLifecycleWhatsAppTemplateMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationLifecycleWhatsAppTemplateMappings_Companies_Comp~",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLifecycleWhatsAppTemplateMappings_WhatsAppIntegr~",
                        column: x => x.WhatsAppIntegrationId,
                        principalTable: "WhatsAppIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLifecycleWhatsAppTemplateMappings_WhatsAppTempla~",
                        column: x => x.WhatsAppTemplateId,
                        principalTable: "WhatsAppTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLifecycleWhatsAppTemplateMappings_WhatsAppIntegr~",
                table: "ReservationLifecycleWhatsAppTemplateMappings",
                column: "WhatsAppIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLifecycleWhatsAppTemplateMappings_WhatsAppTempla~",
                table: "ReservationLifecycleWhatsAppTemplateMappings",
                column: "WhatsAppTemplateId");

            migrationBuilder.CreateIndex(
                name: "UX_ReservationLifecycleWhatsAppTemplateMappings_Company_Integration_EventType_Language",
                table: "ReservationLifecycleWhatsAppTemplateMappings",
                columns: new[] { "CompanyId", "WhatsAppIntegrationId", "JourneyEventType", "LanguageCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationLifecycleWhatsAppTemplateMappings");
        }
    }
}
