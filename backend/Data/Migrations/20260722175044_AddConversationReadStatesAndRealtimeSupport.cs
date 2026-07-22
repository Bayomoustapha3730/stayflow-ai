using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationReadStatesAndRealtimeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationParticipantReadStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReadMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationParticipantReadStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationParticipantReadStates_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationParticipantReadStates_Conversations_Conversatio~",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipantReadStates_CompanyId",
                table: "ConversationParticipantReadStates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipantReadStates_CompanyId_ConversationId_~",
                table: "ConversationParticipantReadStates",
                columns: new[] { "CompanyId", "ConversationId", "ParticipantKind", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipantReadStates_CompanyId_ParticipantKind~",
                table: "ConversationParticipantReadStates",
                columns: new[] { "CompanyId", "ParticipantKind", "ParticipantId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipantReadStates_ConversationId",
                table: "ConversationParticipantReadStates",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationParticipantReadStates");
        }
    }
}
