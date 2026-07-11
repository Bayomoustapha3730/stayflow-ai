using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_CompanyId_ConversationId_CreatedAt",
                table: "ConversationMessages");

            migrationBuilder.RenameColumn(
                name: "Body",
                table: "ConversationMessages",
                newName: "Content");

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                table: "Conversations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedUserId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelIdentity",
                table: "Conversations",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EscalationReason",
                table: "Conversations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HumanTakeoverEnabled",
                table: "Conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Conversations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastActivityAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartedAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "Conversations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "ConversationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedBy",
                table: "ConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalMessageId",
                table: "ConversationMessages",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailureCategory",
                table: "ConversationMessages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ConversationMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MessageType",
                table: "ConversationMessages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Text");

            migrationBuilder.AddColumn<string>(
                name: "ProviderModel",
                table: "ConversationMessages",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "ConversationMessages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRequestId",
                table: "ConversationMessages",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SentAt",
                table: "ConversationMessages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql("""
                UPDATE "Conversations"
                SET "StartedAt" = "CreatedAt",
                    "LastActivityAt" = "UpdatedAt"
                WHERE "StartedAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);

            migrationBuilder.Sql("""
                UPDATE "ConversationMessages"
                SET "SentAt" = "CreatedAt"
                WHERE "SentAt" = TIMESTAMPTZ '0001-01-01 00:00:00+00';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedUserId",
                table: "Conversations",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_Channel_ChannelIdentity",
                table: "Conversations",
                columns: new[] { "CompanyId", "Channel", "ChannelIdentity" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_GuestId_Status",
                table: "Conversations",
                columns: new[] { "CompanyId", "GuestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_Status_LastActivityAt",
                table: "Conversations",
                columns: new[] { "CompanyId", "Status", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_IsDeleted",
                table: "Conversations",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_LastActivityAt",
                table: "Conversations",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_Status",
                table: "Conversations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId_ConversationId_SentAt",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId_ExternalMessageId",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "ExternalMessageId" },
                unique: true,
                filter: "\"ExternalMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId_SentAt",
                table: "ConversationMessages",
                columns: new[] { "ConversationId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ExternalMessageId",
                table: "ConversationMessages",
                column: "ExternalMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_IsDeleted",
                table: "ConversationMessages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SentAt",
                table: "ConversationMessages",
                column: "SentAt");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_AssignedUserId",
                table: "Conversations",
                column: "AssignedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_AssignedUserId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_AssignedUserId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_CompanyId_Channel_ChannelIdentity",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_CompanyId_GuestId_Status",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_CompanyId_Status_LastActivityAt",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_IsDeleted",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_LastActivityAt",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_Status",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_CompanyId_ConversationId_SentAt",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_CompanyId_ExternalMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_ConversationId_SentAt",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_ExternalMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_IsDeleted",
                table: "ConversationMessages");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMessages_SentAt",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ChannelIdentity",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EscalationReason",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "HumanTakeoverEnabled",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ExternalMessageId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "FailureCategory",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ProviderModel",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "ProviderRequestId",
                table: "ConversationMessages");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "ConversationMessages");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "ConversationMessages",
                newName: "Body");

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyId",
                table: "Conversations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId_ConversationId_CreatedAt",
                table: "ConversationMessages",
                columns: new[] { "CompanyId", "ConversationId", "CreatedAt" });
        }
    }
}
