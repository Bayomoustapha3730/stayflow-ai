using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationMessageKnowledgeSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMessageKnowledgeSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyKnowledgeArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    RelevanceReason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessageKnowledgeSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessageKnowledgeSources_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessageKnowledgeSources_ConversationMessages_Co~",
                        column: x => x.ConversationMessageId,
                        principalTable: "ConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMessageKnowledgeSources_Conversations_Conversat~",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMessageKnowledgeSources_PropertyKnowledgeArticl~",
                        column: x => x.PropertyKnowledgeArticleId,
                        principalTable: "PropertyKnowledgeArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageKnowledgeSources_CompanyId",
                table: "ConversationMessageKnowledgeSources",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageKnowledgeSources_CompanyId_ConversationI~",
                table: "ConversationMessageKnowledgeSources",
                columns: new[] { "CompanyId", "ConversationId", "ConversationMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageKnowledgeSources_ConversationId",
                table: "ConversationMessageKnowledgeSources",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageKnowledgeSources_ConversationMessageId",
                table: "ConversationMessageKnowledgeSources",
                column: "ConversationMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageKnowledgeSources_ConversationMessageId_P~",
                table: "ConversationMessageKnowledgeSources",
                columns: new[] { "ConversationMessageId", "PropertyKnowledgeArticleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageKnowledgeSources_ConversationMessageId_R~",
                table: "ConversationMessageKnowledgeSources",
                columns: new[] { "ConversationMessageId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageKnowledgeSources_PropertyKnowledgeArticl~",
                table: "ConversationMessageKnowledgeSources",
                column: "PropertyKnowledgeArticleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMessageKnowledgeSources");
        }
    }
}
