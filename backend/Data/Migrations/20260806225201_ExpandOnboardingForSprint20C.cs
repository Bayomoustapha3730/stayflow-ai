using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandOnboardingForSprint20C : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompletedByUserId",
                table: "OnboardingProgress",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletedStepsCsv",
                table: "OnboardingProgress",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkippedStepsCsv",
                table: "OnboardingProgress",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAtUtc",
                table: "OnboardingProgress",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "OnboardingProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OnboardingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Step = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MetadataJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingEvents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OnboardingEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingProgress_CompletedByUserId",
                table: "OnboardingProgress",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingEvents_CompanyId",
                table: "OnboardingEvents",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingEvents_CompanyId_CreatedAt",
                table: "OnboardingEvents",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingEvents_EventName",
                table: "OnboardingEvents",
                column: "EventName");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingEvents_UserId",
                table: "OnboardingEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingEvents");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingProgress_CompletedByUserId",
                table: "OnboardingProgress");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "OnboardingProgress");

            migrationBuilder.DropColumn(
                name: "CompletedStepsCsv",
                table: "OnboardingProgress");

            migrationBuilder.DropColumn(
                name: "SkippedStepsCsv",
                table: "OnboardingProgress");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "OnboardingProgress");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "OnboardingProgress");
        }
    }
}
