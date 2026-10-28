using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20CustomerUsagePricingCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerUsagePriceVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BillingMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UnitQuantity = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerUsagePriceVersions", x => x.Id);
                    table.CheckConstraint("CK_CustomerUsagePriceVersions_BillingMode", "\"BillingMode\" IN ('IncludedOnly', 'PerUnitOverage', 'NotBillable')");
                    table.CheckConstraint("CK_CustomerUsagePriceVersions_Currency", "\"Currency\" = 'USD'");
                    table.CheckConstraint("CK_CustomerUsagePriceVersions_EffectivePeriod", "\"EffectiveToUtc\" IS NULL OR \"EffectiveToUtc\" > \"EffectiveFromUtc\"");
                    table.CheckConstraint("CK_CustomerUsagePriceVersions_Identity", "\"SubscriptionPlanId\" <> '00000000-0000-0000-0000-000000000000' AND \"Metric\" IN ('Users', 'Properties', 'Reservations', 'AiRequests', 'AiTokens', 'WhatsAppMessages', 'ApiRequests', 'StorageBytes', 'FileUploads')");
                    table.CheckConstraint("CK_CustomerUsagePriceVersions_UnitPrice", "\"UnitPrice\" >= 0");
                    table.CheckConstraint("CK_CustomerUsagePriceVersions_UnitQuantity", "\"UnitQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CustomerUsagePriceVersions_SubscriptionPlans_SubscriptionPl~",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUsagePriceVersions_SubscriptionPlanId_Metric_Effect~",
                table: "CustomerUsagePriceVersions",
                columns: new[] { "SubscriptionPlanId", "Metric", "EffectiveFromUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerUsagePriceVersions");
        }
    }
}
