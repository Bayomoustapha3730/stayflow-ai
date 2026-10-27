using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint20ProviderIdentityDurableDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Model",
                table: "AiProviderCallTelemetries",
                newName: "RequestedModel");

            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                table: "AiProviderCallTelemetries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "AiProviderCallTelemetries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DispatchStartedAt",
                table: "AiProviderCallTelemetries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCategory",
                table: "AiProviderCallTelemetries",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleState",
                table: "AiProviderCallTelemetries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "LegacyObserved");

            migrationBuilder.AlterColumn<string>(
                name: "LifecycleState",
                table: "AiProviderCallTelemetries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "LegacyObserved");

            migrationBuilder.AddColumn<string>(
                name: "ProviderRequestId",
                table: "AiProviderCallTelemetries",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedModel",
                table: "AiProviderCallTelemetries",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "DispatchStartedAt",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "ProviderRequestId",
                table: "AiProviderCallTelemetries");

            migrationBuilder.DropColumn(
                name: "ResolvedModel",
                table: "AiProviderCallTelemetries");

            migrationBuilder.RenameColumn(
                name: "RequestedModel",
                table: "AiProviderCallTelemetries",
                newName: "Model");

            migrationBuilder.AlterColumn<string>(
                name: "Outcome",
                table: "AiProviderCallTelemetries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);
        }
    }
}
