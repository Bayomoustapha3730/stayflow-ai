using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestJourneyMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuestJourneyMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationLifecycleEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    JourneyEventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RenderedContent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TemplateParametersJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestJourneyMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestJourneyMessages_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestJourneyMessages_ConversationMessages_ConversationMessa~",
                        column: x => x.ConversationMessageId,
                        principalTable: "ConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestJourneyMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestJourneyMessages_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestJourneyMessages_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestJourneyMessages_ReservationLifecycleEvents_Reservation~",
                        column: x => x.ReservationLifecycleEventId,
                        principalTable: "ReservationLifecycleEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GuestJourneyMessages_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_CompanyId_IdempotencyKey",
                table: "GuestJourneyMessages",
                columns: new[] { "CompanyId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_CompanyId_ReservationId",
                table: "GuestJourneyMessages",
                columns: new[] { "CompanyId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_CompanyId_Status_NextAttemptAtUtc",
                table: "GuestJourneyMessages",
                columns: new[] { "CompanyId", "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_ConversationId",
                table: "GuestJourneyMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_ConversationMessageId",
                table: "GuestJourneyMessages",
                column: "ConversationMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_GuestId",
                table: "GuestJourneyMessages",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_PropertyId",
                table: "GuestJourneyMessages",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_ReservationId",
                table: "GuestJourneyMessages",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestJourneyMessages_ReservationLifecycleEventId",
                table: "GuestJourneyMessages",
                column: "ReservationLifecycleEventId");

            migrationBuilder.CreateIndex(
                name: "UX_GuestJourneyMessages_CompanyId_ReservationLifecycleEventId",
                table: "GuestJourneyMessages",
                columns: new[] { "CompanyId", "ReservationLifecycleEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestJourneyMessages");
        }
    }
}
