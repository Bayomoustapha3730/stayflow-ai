using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryGuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalReservationReference = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReservationSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CheckInDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckOutDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Adults = table.Column<int>(type: "integer", nullable: false),
                    Children = table.Column<int>(type: "integer", nullable: false),
                    TotalGuestCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    BookingAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    SpecialRequests = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InternalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Guests_PrimaryGuestId",
                        column: x => x.PrimaryGuestId,
                        principalTable: "Guests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CheckInDate",
                table: "Reservations",
                column: "CheckInDate");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CheckOutDate",
                table: "Reservations",
                column: "CheckOutDate");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CompanyId",
                table: "Reservations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CompanyId_ConfirmationNumber",
                table: "Reservations",
                columns: new[] { "CompanyId", "ConfirmationNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CompanyId_ReservationSource_ExternalReservatio~",
                table: "Reservations",
                columns: new[] { "CompanyId", "ReservationSource", "ExternalReservationReference" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ConfirmationNumber",
                table: "Reservations",
                column: "ConfirmationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_CreatedAt",
                table: "Reservations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ExternalReservationReference",
                table: "Reservations",
                column: "ExternalReservationReference");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_IsDeleted",
                table: "Reservations",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PrimaryGuestId",
                table: "Reservations",
                column: "PrimaryGuestId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PropertyId",
                table: "Reservations",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reservations");
        }
    }
}
