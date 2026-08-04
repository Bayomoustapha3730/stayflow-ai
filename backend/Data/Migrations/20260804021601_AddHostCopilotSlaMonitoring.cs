using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHostCopilotSlaMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostCopilotSlaAlerts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsEmergency = table.Column<bool>(type: "boolean", nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastGuestMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostCopilotSlaAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostCopilotSlaAlerts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostCopilotSlaAlerts_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostCopilotSlaAlerts_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostCopilotSlaAlerts_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostCopilotSlaAlerts_CompanyId_ConversationId_Status",
                table: "HostCopilotSlaAlerts",
                columns: new[] { "CompanyId", "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HostCopilotSlaAlerts_CompanyId_Status_TriggeredAt",
                table: "HostCopilotSlaAlerts",
                columns: new[] { "CompanyId", "Status", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HostCopilotSlaAlerts_ConversationId",
                table: "HostCopilotSlaAlerts",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_HostCopilotSlaAlerts_PropertyId",
                table: "HostCopilotSlaAlerts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_HostCopilotSlaAlerts_ReservationId",
                table: "HostCopilotSlaAlerts",
                column: "ReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostCopilotSlaAlerts");
        }
    }
}
