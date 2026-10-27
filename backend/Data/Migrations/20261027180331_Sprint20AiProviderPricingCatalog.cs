using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20AiProviderPricingCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviderPricingVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PricingModelKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    InputPrice = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    OutputPrice = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: false),
                    UnitTokenCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderPricingVersions", x => x.Id);
                    table.CheckConstraint("CK_AiProviderPricingVersions_Currency", "\"Currency\" = 'USD'");
                    table.CheckConstraint("CK_AiProviderPricingVersions_EffectivePeriod", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\"");
                    table.CheckConstraint("CK_AiProviderPricingVersions_Identity", "length(\"Provider\") > 0 AND length(\"PricingModelKey\") > 0");
                    table.CheckConstraint("CK_AiProviderPricingVersions_Prices", "\"InputPrice\" >= 0 AND \"OutputPrice\" >= 0");
                    table.CheckConstraint("CK_AiProviderPricingVersions_UnitTokenCount", "\"UnitTokenCount\" > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderPricingVersions_Provider_PricingModelKey_Effectiv~",
                table: "AiProviderPricingVersions",
                columns: new[] { "Provider", "PricingModelKey", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviderPricingVersions");
        }
    }
}
