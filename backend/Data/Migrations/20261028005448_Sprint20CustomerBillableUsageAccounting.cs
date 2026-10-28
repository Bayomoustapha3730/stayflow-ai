using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20CustomerBillableUsageAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerBillableUsageOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsageOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedQuantity = table.Column<long>(type: "bigint", nullable: false),
                    UsageBefore = table.Column<long>(type: "bigint", nullable: false),
                    UsageAfter = table.Column<long>(type: "bigint", nullable: false),
                    IncludedQuantity = table.Column<long>(type: "bigint", nullable: true),
                    BillableQuantity = table.Column<long>(type: "bigint", nullable: false),
                    BillingState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BillingReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CustomerUsagePriceVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    UnitQuantity = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    ChargeAmount = table.Column<decimal>(type: "numeric(28,18)", precision: 28, scale: 18, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBillableUsageOperations", x => x.Id);
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_BillableQuantity", "\"BillableQuantity\" >= 0 AND \"BillableQuantity\" <= \"ConsumedQuantity\"");
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_BillingState", "(\"BillingState\" = 'Calculated' AND \"BillingReason\" = 'Calculated' AND \"BillableQuantity\" > 0 AND \"CustomerUsagePriceVersionId\" IS NOT NULL AND \"UnitQuantity\" IS NOT NULL AND \"UnitQuantity\" > 0 AND \"UnitPrice\" IS NOT NULL AND \"UnitPrice\" >= 0 AND \"ChargeAmount\" IS NOT NULL AND \"ChargeAmount\" >= 0 AND \"Currency\" = 'USD') OR (\"BillingState\" = 'NotBillable' AND \"BillingReason\" IN ('WithinIncludedAllowance', 'IncludedOnly', 'MetricNotBillable') AND \"ChargeAmount\" IS NULL AND \"Currency\" IS NULL) OR (\"BillingState\" = 'Unknown' AND \"BillingReason\" IN ('MissingPrice', 'AmbiguousPrice', 'UnsupportedBillingMode', 'MissingEntitlement') AND \"BillableQuantity\" > 0 AND \"ChargeAmount\" IS NULL AND \"Currency\" IS NULL)");
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_ChargeAmount", "\"ChargeAmount\" IS NULL OR \"ChargeAmount\" >= 0");
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_Currency", "\"Currency\" IS NULL OR \"Currency\" = 'USD'");
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_Identity", "\"CompanyId\" <> '00000000-0000-0000-0000-000000000000' AND \"TenantSubscriptionId\" <> '00000000-0000-0000-0000-000000000000' AND \"SubscriptionPlanId\" <> '00000000-0000-0000-0000-000000000000' AND \"Metric\" IN ('Users', 'Properties', 'Reservations', 'AiRequests', 'AiTokens', 'WhatsAppMessages', 'ApiRequests', 'StorageBytes', 'FileUploads')");
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_Intervals", "\"PeriodEndUtc\" > \"PeriodStartUtc\" AND \"OccurredAtUtc\" >= \"PeriodStartUtc\" AND \"OccurredAtUtc\" <= \"PeriodEndUtc\"");
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_Quantities", "\"ConsumedQuantity\" > 0 AND \"UsageBefore\" >= 0 AND \"UsageAfter\" = \"UsageBefore\" + \"ConsumedQuantity\" AND (\"IncludedQuantity\" IS NULL OR \"IncludedQuantity\" >= 0)");
                    table.CheckConstraint("CK_CustomerBillableUsageOperations_UnitPrice", "(\"UnitQuantity\" IS NULL AND \"UnitPrice\" IS NULL) OR (\"UnitQuantity\" IS NOT NULL AND \"UnitQuantity\" > 0 AND \"UnitPrice\" IS NOT NULL AND \"UnitPrice\" >= 0)");
                    table.ForeignKey(
                        name: "FK_CustomerBillableUsageOperations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillableUsageOperations_CustomerUsagePriceVersions_~",
                        column: x => x.CustomerUsagePriceVersionId,
                        principalTable: "CustomerUsagePriceVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillableUsageOperations_SubscriptionPlans_Subscript~",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillableUsageOperations_TenantSubscriptions_TenantS~",
                        column: x => x.TenantSubscriptionId,
                        principalTable: "TenantSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillableUsageOperations_UsageOperations_UsageOperat~",
                        column: x => x.UsageOperationId,
                        principalTable: "UsageOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillableUsageOperations_CompanyId_PeriodStartUtc_Me~",
                table: "CustomerBillableUsageOperations",
                columns: new[] { "CompanyId", "PeriodStartUtc", "Metric" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillableUsageOperations_CustomerUsagePriceVersionId",
                table: "CustomerBillableUsageOperations",
                column: "CustomerUsagePriceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillableUsageOperations_SubscriptionPlanId",
                table: "CustomerBillableUsageOperations",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillableUsageOperations_TenantSubscriptionId",
                table: "CustomerBillableUsageOperations",
                column: "TenantSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "UX_CustomerBillableUsageOperations_UsageOperationId",
                table: "CustomerBillableUsageOperations",
                column: "UsageOperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerBillableUsageOperations");
        }
    }
}
