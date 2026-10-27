using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiProviderCallTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviderCallTelemetries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderCallId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OuterOperationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    OuterOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    UsageKnown = table.Column<bool>(type: "boolean", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: true),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: true),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderCallTelemetries", x => x.Id);
                    table.CheckConstraint("CK_AiProviderCallTelemetries_OuterOperation", "(\"OuterOperationType\" IS NULL AND \"OuterOperationId\" IS NULL) OR (\"OuterOperationType\" IS NOT NULL AND \"OuterOperationId\" IS NOT NULL)");
                    table.CheckConstraint("CK_AiProviderCallTelemetries_Usage", "(\"UsageKnown\" = FALSE AND \"InputTokens\" IS NULL AND \"OutputTokens\" IS NULL AND \"TotalTokens\" IS NULL) OR (\"UsageKnown\" = TRUE AND \"InputTokens\" IS NOT NULL AND \"OutputTokens\" IS NOT NULL AND \"TotalTokens\" IS NOT NULL AND \"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"TotalTokens\" >= 0)");
                    table.ForeignKey(
                        name: "FK_AiProviderCallTelemetries_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCallTelemetries_CompanyId_OuterOperationType_Oute~",
                table: "AiProviderCallTelemetries",
                columns: new[] { "CompanyId", "OuterOperationType", "OuterOperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiProviderCallTelemetries_CreatedAt",
                table: "AiProviderCallTelemetries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "UX_AiProviderCallTelemetries_ProviderCallId",
                table: "AiProviderCallTelemetries",
                column: "ProviderCallId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiProviderCallTelemetries");
        }
    }
}
