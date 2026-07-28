using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationMessageFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMessageFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackValue = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessageFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessageFeedback_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMessageFeedback_ConversationMessages_Conversati~",
                        column: x => x.ConversationMessageId,
                        principalTable: "ConversationMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMessageFeedback_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageFeedback_CompanyId",
                table: "ConversationMessageFeedback",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageFeedback_CompanyId_CreatedAt",
                table: "ConversationMessageFeedback",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageFeedback_ConversationId",
                table: "ConversationMessageFeedback",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageFeedback_ConversationMessageId",
                table: "ConversationMessageFeedback",
                column: "ConversationMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageFeedback_ConversationMessageId_GuestId",
                table: "ConversationMessageFeedback",
                columns: new[] { "ConversationMessageId", "GuestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessageFeedback_GuestId",
                table: "ConversationMessageFeedback",
                column: "GuestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMessageFeedback");
        }
    }
}
