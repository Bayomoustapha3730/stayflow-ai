using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeCopilotOperationsReplaySafe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessResultJson",
                table: "CopilotOperations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DispatchStartedAt",
                table: "CopilotOperations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "CopilotOperations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureMessage",
                table: "CopilotOperations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SemanticRequestFingerprint",
                table: "CopilotOperations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BusinessResultJson",
                table: "CopilotOperations");

            migrationBuilder.DropColumn(
                name: "DispatchStartedAt",
                table: "CopilotOperations");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "CopilotOperations");

            migrationBuilder.DropColumn(
                name: "FailureMessage",
                table: "CopilotOperations");

            migrationBuilder.DropColumn(
                name: "SemanticRequestFingerprint",
                table: "CopilotOperations");
        }
    }
}
