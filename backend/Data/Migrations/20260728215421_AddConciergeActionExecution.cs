using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConciergeActionExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionNotificationOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PayloadReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionNotificationOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionNotificationOutbox_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EarlyCheckInRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    GuestNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EarlyCheckInRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EarlyCheckInRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EarlyCheckInRequests_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EarlyCheckInRequests_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EarlyCheckInRequests_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExtraItemRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    GuestNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtraItemRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtraItemRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExtraItemRequests_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExtraItemRequests_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExtraItemRequests_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HostNotificationRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    GuestNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostNotificationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HostNotificationRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostNotificationRecords_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostNotificationRecords_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HostNotificationRecords_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HousekeepingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestType = table.Column<int>(type: "integer", nullable: false),
                    RequestedForDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GuestNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousekeepingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousekeepingRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HousekeepingRequests_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HousekeepingRequests_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HousekeepingRequests_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LateCheckoutRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    GuestNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateCheckoutRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LateCheckoutRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateCheckoutRequests_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateCheckoutRequests_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateCheckoutRequests_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    DescriptionSummary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Urgency = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceTickets_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTickets_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTickets_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceTickets_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParkingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleCount = table.Column<int>(type: "integer", nullable: false),
                    VehicleDescription = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    RequestedFromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RequestedToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GuestNote = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParkingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParkingRequests_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingRequests_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingRequests_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParkingRequests_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PendingConciergeActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    SerializedNormalizedParameters = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedFromMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingConciergeActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingConciergeActions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingConciergeActions_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingConciergeActions_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingConciergeActions_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConciergeActionAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PendingActionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    ActorType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Channel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ResultCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConciergeActionAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConciergeActionAuditLogs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConciergeActionAuditLogs_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConciergeActionAuditLogs_PendingConciergeActions_PendingAct~",
                        column: x => x.PendingActionId,
                        principalTable: "PendingConciergeActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActionNotificationOutbox_CompanyId",
                table: "ActionNotificationOutbox",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionNotificationOutbox_Status_NextAttemptAt",
                table: "ActionNotificationOutbox",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeActionAuditLogs_CompanyId_ConversationId_CreatedAt",
                table: "ConciergeActionAuditLogs",
                columns: new[] { "CompanyId", "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeActionAuditLogs_ConversationId",
                table: "ConciergeActionAuditLogs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConciergeActionAuditLogs_PendingActionId_CreatedAt",
                table: "ConciergeActionAuditLogs",
                columns: new[] { "PendingActionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EarlyCheckInRequests_CompanyId_PropertyId_Status",
                table: "EarlyCheckInRequests",
                columns: new[] { "CompanyId", "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EarlyCheckInRequests_ConversationId",
                table: "EarlyCheckInRequests",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyCheckInRequests_PropertyId",
                table: "EarlyCheckInRequests",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_EarlyCheckInRequests_ReservationId_CreatedAt",
                table: "EarlyCheckInRequests",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraItemRequests_CompanyId_PropertyId_Status",
                table: "ExtraItemRequests",
                columns: new[] { "CompanyId", "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtraItemRequests_ConversationId",
                table: "ExtraItemRequests",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraItemRequests_PropertyId",
                table: "ExtraItemRequests",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtraItemRequests_ReservationId_CreatedAt",
                table: "ExtraItemRequests",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HostNotificationRecords_CompanyId_PropertyId_CreatedAt",
                table: "HostNotificationRecords",
                columns: new[] { "CompanyId", "PropertyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HostNotificationRecords_ConversationId",
                table: "HostNotificationRecords",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_HostNotificationRecords_PropertyId",
                table: "HostNotificationRecords",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_HostNotificationRecords_ReservationId",
                table: "HostNotificationRecords",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingRequests_CompanyId_PropertyId_Status",
                table: "HousekeepingRequests",
                columns: new[] { "CompanyId", "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingRequests_ConversationId",
                table: "HousekeepingRequests",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingRequests_PropertyId",
                table: "HousekeepingRequests",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_HousekeepingRequests_ReservationId_CreatedAt",
                table: "HousekeepingRequests",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LateCheckoutRequests_CompanyId_PropertyId_Status",
                table: "LateCheckoutRequests",
                columns: new[] { "CompanyId", "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LateCheckoutRequests_ConversationId",
                table: "LateCheckoutRequests",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_LateCheckoutRequests_PropertyId",
                table: "LateCheckoutRequests",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_LateCheckoutRequests_ReservationId_CreatedAt",
                table: "LateCheckoutRequests",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_CompanyId_PropertyId_Status",
                table: "MaintenanceTickets",
                columns: new[] { "CompanyId", "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_ConversationId",
                table: "MaintenanceTickets",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_PropertyId",
                table: "MaintenanceTickets",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_ReservationId_CreatedAt",
                table: "MaintenanceTickets",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequests_CompanyId_PropertyId_Status",
                table: "ParkingRequests",
                columns: new[] { "CompanyId", "PropertyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequests_ConversationId",
                table: "ParkingRequests",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequests_PropertyId",
                table: "ParkingRequests",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_ParkingRequests_ReservationId_CreatedAt",
                table: "ParkingRequests",
                columns: new[] { "ReservationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingConciergeActions_CompanyId_ConversationId_Status",
                table: "PendingConciergeActions",
                columns: new[] { "CompanyId", "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingConciergeActions_ConversationId",
                table: "PendingConciergeActions",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingConciergeActions_ExpiresAt",
                table: "PendingConciergeActions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PendingConciergeActions_IdempotencyKey",
                table: "PendingConciergeActions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PendingConciergeActions_PropertyId",
                table: "PendingConciergeActions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingConciergeActions_ReservationId",
                table: "PendingConciergeActions",
                column: "ReservationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionNotificationOutbox");

            migrationBuilder.DropTable(
                name: "ConciergeActionAuditLogs");

            migrationBuilder.DropTable(
                name: "EarlyCheckInRequests");

            migrationBuilder.DropTable(
                name: "ExtraItemRequests");

            migrationBuilder.DropTable(
                name: "HostNotificationRecords");

            migrationBuilder.DropTable(
                name: "HousekeepingRequests");

            migrationBuilder.DropTable(
                name: "LateCheckoutRequests");

            migrationBuilder.DropTable(
                name: "MaintenanceTickets");

            migrationBuilder.DropTable(
                name: "ParkingRequests");

            migrationBuilder.DropTable(
                name: "PendingConciergeActions");
        }
    }
}
