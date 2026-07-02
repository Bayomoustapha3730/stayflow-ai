using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamePropertyDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "Amenities",
                newTableName: "PropertyAmenities",
                oldPrimaryKeyName: "PK_Amenities",
                newPrimaryKeyName: "PK_PropertyAmenities",
                oldForeignKeyName: "FK_Amenities_Properties_PropertyId",
                newForeignKeyName: "FK_PropertyAmenities_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_Amenities_CreatedAt", "IX_PropertyAmenities_CreatedAt"),
                    ("IX_Amenities_PropertyId", "IX_PropertyAmenities_PropertyId")
                ]);

            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "EmergencyContacts",
                newTableName: "PropertyEmergencyContacts",
                oldPrimaryKeyName: "PK_EmergencyContacts",
                newPrimaryKeyName: "PK_PropertyEmergencyContacts",
                oldForeignKeyName: "FK_EmergencyContacts_Properties_PropertyId",
                newForeignKeyName: "FK_PropertyEmergencyContacts_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_EmergencyContacts_CreatedAt", "IX_PropertyEmergencyContacts_CreatedAt"),
                    ("IX_EmergencyContacts_PhoneNumber", "IX_PropertyEmergencyContacts_PhoneNumber"),
                    ("IX_EmergencyContacts_PropertyId", "IX_PropertyEmergencyContacts_PropertyId")
                ]);

            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "HouseRules",
                newTableName: "PropertyHouseRules",
                oldPrimaryKeyName: "PK_HouseRules",
                newPrimaryKeyName: "PK_PropertyHouseRules",
                oldForeignKeyName: "FK_HouseRules_Properties_PropertyId",
                newForeignKeyName: "FK_PropertyHouseRules_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_HouseRules_CreatedAt", "IX_PropertyHouseRules_CreatedAt"),
                    ("IX_HouseRules_PropertyId", "IX_PropertyHouseRules_PropertyId")
                ]);

            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "LocalRecommendations",
                newTableName: "PropertyRecommendations",
                oldPrimaryKeyName: "PK_LocalRecommendations",
                newPrimaryKeyName: "PK_PropertyRecommendations",
                oldForeignKeyName: "FK_LocalRecommendations_Properties_PropertyId",
                newForeignKeyName: "FK_PropertyRecommendations_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_LocalRecommendations_CreatedAt", "IX_PropertyRecommendations_CreatedAt"),
                    ("IX_LocalRecommendations_PropertyId", "IX_PropertyRecommendations_PropertyId")
                ]);

            migrationBuilder.CreateTable(
                name: "PropertyKnowledgeArticles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyKnowledgeArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyKnowledgeArticles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PropertyKnowledgeArticles_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_CompanyId",
                table: "PropertyKnowledgeArticles",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_CreatedAt",
                table: "PropertyKnowledgeArticles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyKnowledgeArticles_PropertyId",
                table: "PropertyKnowledgeArticles",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyKnowledgeArticles");

            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "PropertyAmenities",
                newTableName: "Amenities",
                oldPrimaryKeyName: "PK_PropertyAmenities",
                newPrimaryKeyName: "PK_Amenities",
                oldForeignKeyName: "FK_PropertyAmenities_Properties_PropertyId",
                newForeignKeyName: "FK_Amenities_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_PropertyAmenities_CreatedAt", "IX_Amenities_CreatedAt"),
                    ("IX_PropertyAmenities_PropertyId", "IX_Amenities_PropertyId")
                ]);

            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "PropertyEmergencyContacts",
                newTableName: "EmergencyContacts",
                oldPrimaryKeyName: "PK_PropertyEmergencyContacts",
                newPrimaryKeyName: "PK_EmergencyContacts",
                oldForeignKeyName: "FK_PropertyEmergencyContacts_Properties_PropertyId",
                newForeignKeyName: "FK_EmergencyContacts_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_PropertyEmergencyContacts_CreatedAt", "IX_EmergencyContacts_CreatedAt"),
                    ("IX_PropertyEmergencyContacts_PhoneNumber", "IX_EmergencyContacts_PhoneNumber"),
                    ("IX_PropertyEmergencyContacts_PropertyId", "IX_EmergencyContacts_PropertyId")
                ]);

            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "PropertyHouseRules",
                newTableName: "HouseRules",
                oldPrimaryKeyName: "PK_PropertyHouseRules",
                newPrimaryKeyName: "PK_HouseRules",
                oldForeignKeyName: "FK_PropertyHouseRules_Properties_PropertyId",
                newForeignKeyName: "FK_HouseRules_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_PropertyHouseRules_CreatedAt", "IX_HouseRules_CreatedAt"),
                    ("IX_PropertyHouseRules_PropertyId", "IX_HouseRules_PropertyId")
                ]);

            RenamePropertyChildTable(
                migrationBuilder,
                oldTableName: "PropertyRecommendations",
                newTableName: "LocalRecommendations",
                oldPrimaryKeyName: "PK_PropertyRecommendations",
                newPrimaryKeyName: "PK_LocalRecommendations",
                oldForeignKeyName: "FK_PropertyRecommendations_Properties_PropertyId",
                newForeignKeyName: "FK_LocalRecommendations_Properties_PropertyId",
                indexRenames:
                [
                    ("IX_PropertyRecommendations_CreatedAt", "IX_LocalRecommendations_CreatedAt"),
                    ("IX_PropertyRecommendations_PropertyId", "IX_LocalRecommendations_PropertyId")
                ]);
        }

        private static void RenamePropertyChildTable(
            MigrationBuilder migrationBuilder,
            string oldTableName,
            string newTableName,
            string oldPrimaryKeyName,
            string newPrimaryKeyName,
            string oldForeignKeyName,
            string newForeignKeyName,
            IReadOnlyCollection<(string OldName, string NewName)> indexRenames)
        {
            migrationBuilder.DropForeignKey(
                name: oldForeignKeyName,
                table: oldTableName);

            migrationBuilder.DropPrimaryKey(
                name: oldPrimaryKeyName,
                table: oldTableName);

            migrationBuilder.RenameTable(
                name: oldTableName,
                newName: newTableName);

            foreach (var (oldIndexName, newIndexName) in indexRenames)
            {
                migrationBuilder.RenameIndex(
                    name: oldIndexName,
                    table: newTableName,
                    newName: newIndexName);
            }

            migrationBuilder.AddPrimaryKey(
                name: newPrimaryKeyName,
                table: newTableName,
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: newForeignKeyName,
                table: newTableName,
                column: "PropertyId",
                principalTable: "Properties",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
