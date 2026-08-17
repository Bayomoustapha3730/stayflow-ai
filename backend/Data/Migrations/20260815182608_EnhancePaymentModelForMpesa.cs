using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnhancePaymentModelForMpesa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerPhoneNumber",
                table: "Payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FailedAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "Payments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "Payments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalReference",
                table: "Payments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Payments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Payments",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderCheckoutRequestId",
                table: "Payments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderEnvironment",
                table: "Payments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderRequestId",
                table: "Payments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderTransactionId",
                table: "Payments",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RequestedAtUtc",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(
                        type: "character varying(40)",
                        maxLength: 40,
                        nullable: false),
                    EventId = table.Column<string>(
                        type: "character varying(160)",
                        maxLength: 160,
                        nullable: false),
                    EventType = table.Column<string>(
                        type: "character varying(120)",
                        maxLength: 120,
                        nullable: false),
                    CheckoutRequestId = table.Column<string>(
                        type: "character varying(160)",
                        maxLength: 160,
                        nullable: true),
                    TransactionId = table.Column<string>(
                        type: "character varying(160)",
                        maxLength: 160,
                        nullable: true),
                    EventCreatedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false),
                    PayloadHash = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false),
                    WasDuplicate = table.Column<bool>(
                        type: "boolean",
                        nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompanyId_GuestId",
                table: "Payments",
                columns: new[] { "CompanyId", "GuestId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompanyId_ExternalReference",
                table: "Payments",
                columns: new[] { "CompanyId", "ExternalReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CompanyId_ReservationId",
                table: "Payments",
                columns: new[] { "CompanyId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider",
                table: "Payments",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderCheckoutRequestId",
                table: "Payments",
                column: "ProviderCheckoutRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ProviderTransactionId",
                table: "Payments",
                column: "ProviderTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_CheckoutRequestId",
                table: "PaymentWebhookEvents",
                column: "CheckoutRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_EventCreatedAtUtc",
                table: "PaymentWebhookEvents",
                column: "EventCreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_Provider_EventId",
                table: "PaymentWebhookEvents",
                columns: new[] { "Provider", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentWebhookEvents_TransactionId",
                table: "PaymentWebhookEvents",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments",
                column: "ReservationId",
                principalTable: "Reservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Reservations_ReservationId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "PaymentWebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CompanyId_GuestId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CompanyId_ExternalReference",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CompanyId_ReservationId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Provider",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ProviderCheckoutRequestId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ProviderTransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReservationId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CustomerPhoneNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "InternalReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderCheckoutRequestId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderEnvironment",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderRequestId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderTransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RequestedAtUtc",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                table: "Payments");
        }
    }
}
