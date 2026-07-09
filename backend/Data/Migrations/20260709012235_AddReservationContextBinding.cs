using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReservationContextBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReservationContextBoundAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationContextResolutionMethod",
                table: "Conversations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ReservationId",
                table: "Conversations",
                column: "ReservationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Reservations_ReservationId",
                table: "Conversations",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Reservations_ReservationId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ReservationId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ReservationContextBoundAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ReservationContextResolutionMethod",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Conversations");
        }
    }
}
