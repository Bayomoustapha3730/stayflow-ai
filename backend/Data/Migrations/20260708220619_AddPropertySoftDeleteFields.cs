using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertySoftDeleteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Properties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "Properties",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "DeletedAt", "DeletedBy", "IsDeleted" },
                values: new object[] { null, null, false });

            migrationBuilder.CreateIndex(
                name: "IX_Properties_IsDeleted",
                table: "Properties",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Properties_IsDeleted",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Properties");
        }
    }
}
