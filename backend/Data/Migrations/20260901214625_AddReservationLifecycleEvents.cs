using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationLifecycleEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservationLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RuleVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PropertyLocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledForUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservationLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservationLifecycleEvents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLifecycleEvents_Guests_GuestId",
                        column: x => x.GuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLifecycleEvents_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservationLifecycleEvents_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLifecycleEvents_CompanyId_ReservationId",
                table: "ReservationLifecycleEvents",
                columns: new[] { "CompanyId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLifecycleEvents_CompanyId_Status_ScheduledForUtc",
                table: "ReservationLifecycleEvents",
                columns: new[] { "CompanyId", "Status", "ScheduledForUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLifecycleEvents_GuestId",
                table: "ReservationLifecycleEvents",
                column: "GuestId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLifecycleEvents_PropertyId",
                table: "ReservationLifecycleEvents",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservationLifecycleEvents_ReservationId",
                table: "ReservationLifecycleEvents",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "UX_ReservationLifecycleEvents_CompanyId_IdempotencyKey",
                table: "ReservationLifecycleEvents",
                columns: new[] { "CompanyId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservationLifecycleEvents");
        }
    }
}
