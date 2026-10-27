using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20AiProviderCallCostAccounting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CostReason",
                table: "AiProviderCallTelemetries",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ProviderUsageUnavailable");

            migrationBuilder.AddColumn<string>(
                name: "CostState",
                table: "AiProviderCallTelemetries",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<Guid>(
                name: "PricingVersionId",
                table: "AiProviderCallTelemetries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderCostAmount",
                table: "AiProviderCallTelemetries",
                type: "numeric(28,18)",
                precision: 28,
                scale: 18,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCostCurrency",
                table: "AiProviderCallTelemetries",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCallTelemetries_PricingVersionId",
                table: "AiProviderCallTelemetries",
                column: "PricingVersionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostAmount",
                table: "AiProviderCallTelemetries",
                sql: "\"ProviderCostAmount\" IS NULL OR \"ProviderCostAmount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostCurrency",
                table: "AiProviderCallTelemetries",
                sql: "\"ProviderCostCurrency\" IS NULL OR \"ProviderCostCurrency\" = 'USD'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostReason",
                table: "AiProviderCallTelemetries",
                sql: "\"CostReason\" IN ('Calculated', 'MissingResolvedModel', 'MissingDispatchTimestamp', 'UnknownProvider', 'UnknownModel', 'NoEffectivePrice', 'AmbiguousPrice', 'MissingTokenUsage', 'UnsupportedPricingDimension', 'ProviderUsageUnavailable', 'NonBillableProvider', 'CalculationOverflow')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostState",
                table: "AiProviderCallTelemetries",
                sql: "(\"CostState\" = 'Calculated' AND \"CostReason\" = 'Calculated' AND \"PricingVersionId\" IS NOT NULL AND \"ProviderCostAmount\" IS NOT NULL AND \"ProviderCostCurrency\" = 'USD') OR (\"CostState\" = 'Unknown' AND \"CostReason\" NOT IN ('Calculated', 'NonBillableProvider') AND \"ProviderCostAmount\" IS NULL AND \"ProviderCostCurrency\" IS NULL) OR (\"CostState\" = 'NotApplicable' AND \"CostReason\" = 'NonBillableProvider' AND \"PricingVersionId\" IS NULL AND \"ProviderCostAmount\" IS NULL AND \"ProviderCostCurrency\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_AiProviderCallTelemetries_AiProviderPricingVersions_Pricing~",
                table: "AiProviderCallTelemetries",
                column: "PricingVersionId",
                principalTable: "AiProviderPricingVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiProviderCallTelemetries_AiProviderPricingVersions_Pricing~",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropIndex(
                name: "IX_AiProviderCallTelemetries_PricingVersionId",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostAmount",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostCurrency",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostReason",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiProviderCallTelemetries_CostState",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "CostReason",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "CostState",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "PricingVersionId",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "ProviderCostAmount",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "ProviderCostCurrency",
                table: "AiProviderCallTelemetries");
        }
    }
}
