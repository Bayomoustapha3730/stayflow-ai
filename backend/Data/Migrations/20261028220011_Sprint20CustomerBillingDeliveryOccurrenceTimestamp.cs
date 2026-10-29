using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20CustomerBillingDeliveryOccurrenceTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OccurredAtUtc",
                table: "CustomerBillingDeliveries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "CustomerBillingDeliveries" AS delivery
                SET "OccurredAtUtc" = operation."OccurredAtUtc"
                FROM "CustomerBillableUsageOperations" AS operation
                WHERE delivery."CustomerBillableUsageOperationId" = operation."Id";
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "OccurredAtUtc",
                table: "CustomerBillingDeliveries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CustomerBillingDeliveries_OccurredAt",
                table: "CustomerBillingDeliveries",
                sql: "\"OccurredAtUtc\" >= \"PeriodStartUtc\" AND \"OccurredAtUtc\" <= \"PeriodEndUtc\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CustomerBillingDeliveries_OccurredAt",
                table: "CustomerBillingDeliveries");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                table: "CustomerBillingDeliveries");
        }
    }
}
