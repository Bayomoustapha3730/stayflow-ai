using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_CreatedAt",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "PropertyKnowledgeArticles",
                type: "character varying(6000)",
                maxLength: 6000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovedAt",
                table: "PropertyKnowledgeArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "PropertyKnowledgeArticles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "PropertyKnowledgeArticles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "PropertyKnowledgeArticles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "PropertyKnowledgeArticles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedByUserId",
                table: "PropertyKnowledgeArticles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "PropertyKnowledgeArticles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PropertyKnowledgeArticles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "PropertyKnowledgeArticles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "PropertyKnowledgeArticles",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "PropertyKnowledgeArticles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByUserId",
                table: "PropertyKnowledgeArticles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_ApprovedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_Category",
                table: "PropertyKnowledgeArticles",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_CompanyId_PropertyId_IsApproved_I~",
                table: "PropertyKnowledgeArticles",
                columns: new[] { "CompanyId", "PropertyId", "IsApproved", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_CreatedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_DeletedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_IsActive",
                table: "PropertyKnowledgeArticles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_IsApproved",
                table: "PropertyKnowledgeArticles",
                column: "IsApproved");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_UpdatedAt",
                table: "PropertyKnowledgeArticles",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_UpdatedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_ApprovedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_CreatedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_DeletedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_UpdatedByUserId",
                table: "PropertyKnowledgeArticles",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_ApprovedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_CreatedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_DeletedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropForeignKey(
                name: "FK_PropertyKnowledgeArticles_Users_UpdatedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_ApprovedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_Category",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_CompanyId_PropertyId_IsApproved_I~",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_CreatedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_DeletedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_IsActive",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_IsApproved",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_UpdatedAt",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropIndex(
                name: "IX_PropertyKnowledgeArticles_UpdatedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "PropertyKnowledgeArticles");

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "PropertyKnowledgeArticles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(6000)",
                oldMaxLength: 6000);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_CreatedAt",
                table: "PropertyKnowledgeArticles",
                column: "CreatedAt");
        }
    }
}
