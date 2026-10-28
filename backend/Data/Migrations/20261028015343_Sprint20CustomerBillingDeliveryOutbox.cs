using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20CustomerBillingDeliveryOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerBillingDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerBillableUsageOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSubscriptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PeriodStartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveryProvider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeliveryKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BillingStateSnapshot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BillingReasonSnapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BillableQuantitySnapshot = table.Column<long>(type: "bigint", nullable: false),
                    ChargeAmountSnapshot = table.Column<decimal>(type: "numeric(28,18)", precision: 28, scale: 18, nullable: false),
                    CurrencySnapshot = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CustomerUsagePriceVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeliveryReason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternalIdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExternalObjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalRequestId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    FirstAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IndeterminateAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFailureCategory = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBillingDeliveries", x => x.Id);
                    table.CheckConstraint("CK_CustomerBillingDeliveries_AttemptCount", "\"AttemptCount\" >= 0");
                    table.CheckConstraint("CK_CustomerBillingDeliveries_BoundedValues", "\"DeliveryProvider\" = 'Stripe' AND \"DeliveryKind\" = 'PendingExternalBilling' AND \"DeliveryState\" IN ('Prepared', 'Dispatching', 'Delivered', 'Failed', 'Indeterminate', 'Suppressed') AND \"DeliveryReason\" IN ('Prepared', 'DispatchClaimed', 'Delivered', 'ZeroChargeSuppressed', 'ProviderRejected', 'ProviderUnavailable', 'Timeout', 'Canceled', 'NetworkFailure', 'PersistenceFailure', 'IndeterminateExternalOutcome', 'ConfigurationMissing', 'InvalidSubscriptionMapping', 'InvalidPriceMapping') AND \"BillingStateSnapshot\" = 'Calculated' AND \"BillingReasonSnapshot\" = 'Calculated' AND \"Metric\" IN ('Users', 'Properties', 'Reservations', 'AiRequests', 'AiTokens', 'WhatsAppMessages', 'ApiRequests', 'StorageBytes', 'FileUploads')");
                    table.CheckConstraint("CK_CustomerBillingDeliveries_DeliveryTimestamps", "(\"DeliveryState\" = 'Delivered' AND \"DeliveredAtUtc\" IS NOT NULL) OR (\"DeliveryState\" <> 'Delivered' AND \"DeliveredAtUtc\" IS NULL)");
                    table.CheckConstraint("CK_CustomerBillingDeliveries_FailureCategory", "\"LastFailureCategory\" IS NULL OR \"LastFailureCategory\" IN ('ProviderRejected', 'ProviderUnavailable', 'Timeout', 'Canceled', 'NetworkFailure', 'PersistenceFailure', 'ConfigurationMissing', 'InvalidSubscriptionMapping', 'InvalidPriceMapping', 'RetryableProviderUnavailable')");
                    table.CheckConstraint("CK_CustomerBillingDeliveries_FinancialFacts", "\"BillableQuantitySnapshot\" >= 0 AND \"ChargeAmountSnapshot\" >= 0 AND \"CurrencySnapshot\" = 'USD'");
                    table.CheckConstraint("CK_CustomerBillingDeliveries_IdempotencyKey", "length(\"ExternalIdempotencyKey\") > 0");
                    table.CheckConstraint("CK_CustomerBillingDeliveries_Identity", "\"CompanyId\" <> '00000000-0000-0000-0000-000000000000' AND \"CustomerBillableUsageOperationId\" <> '00000000-0000-0000-0000-000000000000' AND \"TenantSubscriptionId\" <> '00000000-0000-0000-0000-000000000000' AND \"SubscriptionPlanId\" <> '00000000-0000-0000-0000-000000000000' AND \"CustomerUsagePriceVersionId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_CustomerBillingDeliveries_Period", "\"PeriodStartUtc\" < \"PeriodEndUtc\"");
                    table.ForeignKey(
                        name: "FK_CustomerBillingDeliveries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillingDeliveries_CustomerBillableUsageOperations_C~",
                        column: x => x.CustomerBillableUsageOperationId,
                        principalTable: "CustomerBillableUsageOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillingDeliveries_CustomerUsagePriceVersions_Custom~",
                        column: x => x.CustomerUsagePriceVersionId,
                        principalTable: "CustomerUsagePriceVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillingDeliveries_SubscriptionPlans_SubscriptionPla~",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBillingDeliveries_TenantSubscriptions_TenantSubscri~",
                        column: x => x.TenantSubscriptionId,
                        principalTable: "TenantSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillingDeliveries_CompanyId",
                table: "CustomerBillingDeliveries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillingDeliveries_CustomerUsagePriceVersionId",
                table: "CustomerBillingDeliveries",
                column: "CustomerUsagePriceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillingDeliveries_DeliveryState_CreatedAtUtc",
                table: "CustomerBillingDeliveries",
                columns: new[] { "DeliveryState", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillingDeliveries_SubscriptionPlanId",
                table: "CustomerBillingDeliveries",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBillingDeliveries_TenantSubscriptionId",
                table: "CustomerBillingDeliveries",
                column: "TenantSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "UX_CustomerBillingDeliveries_BillingOperationProviderKind",
                table: "CustomerBillingDeliveries",
                columns: new[] { "CustomerBillableUsageOperationId", "DeliveryProvider", "DeliveryKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CustomerBillingDeliveries_ExternalIdempotencyKey",
                table: "CustomerBillingDeliveries",
                column: "ExternalIdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerBillingDeliveries");
        }
    }
}
