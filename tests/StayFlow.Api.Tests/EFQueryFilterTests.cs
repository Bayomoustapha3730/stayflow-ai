using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.Tests;

/// <summary>
/// Regression coverage for PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning
/// cleanup: operational dependents must hide when their governing principal is soft-deleted,
/// while ConciergeActionAuditLog must remain directly queryable as historical evidence.
/// Uses SQLite (not the InMemory provider) so required-navigation joins are executed for real,
/// which is what actually reproduces the Include() silent-drop risk the EF warning is about.
/// </summary>
public sealed class EFQueryFilterTests
{
    [Fact]
    public async Task HousekeepingRequest_WithActiveConversation_IsVisible()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        await fixture.SeedHousekeepingRequestAsync(conversation.Id);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.HousekeepingRequests.CountAsync(item => item.ConversationId == conversation.Id));
    }

    [Fact]
    public async Task HousekeepingRequest_WithSoftDeletedConversation_IsHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        await fixture.SeedHousekeepingRequestAsync(conversation.Id);
        await fixture.SoftDeleteConversationAsync(conversation.Id);

        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.HousekeepingRequests.CountAsync(item => item.ConversationId == conversation.Id));
    }

    [Fact]
    public async Task PendingConciergeAction_WithSoftDeletedGuest_IsHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        await fixture.SeedPendingConciergeActionAsync(conversation.Id);
        await fixture.SoftDeleteGuestAsync(conversation.GuestId);

        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.PendingConciergeActions.CountAsync(item => item.ConversationId == conversation.Id));
    }

    [Fact]
    public async Task MaintenanceTicket_WithSoftDeletedProperty_IsHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        await fixture.SeedMaintenanceTicketAsync(conversation.Id);
        await fixture.SoftDeletePropertyAsync(conversation.PropertyId!.Value);

        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.MaintenanceTickets.CountAsync(item => item.ConversationId == conversation.Id));
    }

    [Fact]
    public async Task ConversationMessageFeedback_WithSoftDeletedProperty_IsHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        await fixture.SeedConversationMessageFeedbackAsync(conversation.Id);
        await fixture.SoftDeletePropertyAsync(conversation.PropertyId!.Value);

        await using var context = fixture.CreateContext();
        var query = context.ConversationMessageFeedback.Where(item => item.ConversationId == conversation.Id);
        Assert.Equal(0, await query.CountAsync());
    }

    [Fact]
    public async Task ConversationMessageKnowledgeSource_WithSoftDeletedProperty_IsHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        await fixture.SeedConversationMessageKnowledgeSourceAsync(conversation.Id);
        await fixture.SoftDeletePropertyAsync(conversation.PropertyId!.Value);

        await using var context = fixture.CreateContext();
        var query = context.ConversationMessageKnowledgeSources.Where(item => item.ConversationId == conversation.Id);
        Assert.Equal(0, await query.CountAsync());
    }

    [Fact]
    public async Task GuestJourneyMessage_WithActiveGuest_IsVisible()
    {
        await using var fixture = await Fixture.CreateAsync();
        var reservation = await fixture.SeedReservationAsync();
        await fixture.SeedGuestJourneyMessageAsync(reservation.Id, reservation.PrimaryGuestId);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.GuestJourneyMessages.CountAsync(item => item.ReservationId == reservation.Id));
    }

    [Fact]
    public async Task GuestJourneyMessage_WithSoftDeletedGuest_IsHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var reservation = await fixture.SeedReservationAsync();
        await fixture.SeedGuestJourneyMessageAsync(reservation.Id, reservation.PrimaryGuestId);
        await fixture.SoftDeleteGuestAsync(reservation.PrimaryGuestId);

        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.GuestJourneyMessages.CountAsync(item => item.ReservationId == reservation.Id));
    }

    [Fact]
    public async Task ReservationLifecycleEvent_WithActiveGuest_IsVisible()
    {
        await using var fixture = await Fixture.CreateAsync();
        var reservation = await fixture.SeedReservationAsync();
        await fixture.SeedReservationLifecycleEventAsync(reservation.Id, reservation.PrimaryGuestId);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.ReservationLifecycleEvents.CountAsync(item => item.ReservationId == reservation.Id));
    }

    [Fact]
    public async Task ReservationLifecycleEvent_WithSoftDeletedGuest_IsHidden()
    {
        await using var fixture = await Fixture.CreateAsync();
        var reservation = await fixture.SeedReservationAsync();
        await fixture.SeedReservationLifecycleEventAsync(reservation.Id, reservation.PrimaryGuestId);
        await fixture.SoftDeleteGuestAsync(reservation.PrimaryGuestId);

        await using var context = fixture.CreateContext();
        Assert.Equal(0, await context.ReservationLifecycleEvents.CountAsync(item => item.ReservationId == reservation.Id));
    }

    [Fact]
    public async Task ConciergeActionAuditLog_RemainsVisible_AfterConversationSoftDeleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        var auditLogId = await fixture.SeedAuditLogAsync(conversation.Id);
        await fixture.SoftDeleteConversationAsync(conversation.Id);

        await using var context = fixture.CreateContext();
        Assert.True(await context.ConciergeActionAuditLogs.AnyAsync(item => item.Id == auditLogId));
    }

    [Fact]
    public async Task ConciergeActionAuditLog_RemainsVisible_AfterGuestSoftDeleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        var auditLogId = await fixture.SeedAuditLogAsync(conversation.Id);
        await fixture.SoftDeleteGuestAsync(conversation.GuestId);

        await using var context = fixture.CreateContext();
        Assert.True(await context.ConciergeActionAuditLogs.AnyAsync(item => item.Id == auditLogId));
    }

    [Fact]
    public async Task ConciergeActionAuditLog_RemainsVisible_AfterPropertySoftDeleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        var auditLogId = await fixture.SeedAuditLogAsync(conversation.Id);
        await fixture.SoftDeletePropertyAsync(conversation.PropertyId!.Value);

        await using var context = fixture.CreateContext();
        Assert.True(await context.ConciergeActionAuditLogs.AnyAsync(item => item.Id == auditLogId));
    }

    [Fact]
    public async Task ConciergeActionAuditLog_Include_Conversation_SilentlyDropsRow_WhenConversationSoftDeleted()
    {
        // Documents the residual risk noted in design rule B: silencing the EF warning on the
        // dependent does not change the fact that Conversation is a required, filtered principal.
        // Include(x => x.Conversation) still performs an inner join and drops the audit row.
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        var auditLogId = await fixture.SeedAuditLogAsync(conversation.Id);
        await fixture.SoftDeleteConversationAsync(conversation.Id);

        await using var context = fixture.CreateContext();
        var withoutInclude = await context.ConciergeActionAuditLogs.AnyAsync(item => item.Id == auditLogId);
        // AnyAsync/CountAsync ignore Include (it only affects materialized results), so the
        // drop must be observed via a materializing query such as ToListAsync/FirstOrDefaultAsync.
        var withInclude = await context.ConciergeActionAuditLogs
            .Include(item => item.Conversation)
            .Where(item => item.Id == auditLogId)
            .ToListAsync();

        Assert.True(withoutInclude);
        Assert.Empty(withInclude);
    }

    [Fact]
    public async Task ConciergeActionAuditLog_IgnoreQueryFilters_RestoresConversationInclude_WhenConversationSoftDeleted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        var auditLogId = await fixture.SeedAuditLogAsync(conversation.Id);
        await fixture.SoftDeleteConversationAsync(conversation.Id);

        await using var context = fixture.CreateContext();
        var audit = await context.ConciergeActionAuditLogs
            .IgnoreQueryFilters()
            .Include(item => item.Conversation)
            .SingleAsync(item => item.Id == auditLogId);

        Assert.NotNull(audit.Conversation);
        Assert.True(audit.Conversation.IsDeleted);
    }

    [Fact]
    public async Task PendingConciergeAction_ActiveConversation_ConciergeWorkflowUnaffected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var conversation = await fixture.SeedConversationAsync();
        await fixture.SeedPendingConciergeActionAsync(conversation.Id);
        await fixture.SeedHousekeepingRequestAsync(conversation.Id);
        await fixture.SeedAuditLogAsync(conversation.Id);

        await using var context = fixture.CreateContext();
        Assert.Equal(1, await context.PendingConciergeActions.CountAsync(item => item.ConversationId == conversation.Id));
        Assert.Equal(1, await context.HousekeepingRequests.CountAsync(item => item.ConversationId == conversation.Id));
        Assert.Equal(1, await context.ConciergeActionAuditLogs.CountAsync(item => item.ConversationId == conversation.Id));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid PropertyId { get; } = Guid.NewGuid();
        public Guid GuestId { get; } = Guid.NewGuid();

        private Fixture(SqliteConnection connection, DbContextOptions<ApplicationDbContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var fixture = new Fixture(connection, options);

            await using var context = fixture.CreateContext();
            await context.Database.EnsureCreatedAsync();

            context.Companies.Add(new Company
            {
                Id = fixture.CompanyId,
                Name = "Test Co",
                Slug = "test-co",
                NormalizedSlug = "TEST-CO",
                Status = "Active",
                Email = "test@example.com",
                PhoneNumber = "+254700000000",
                CountryCode = "KE",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            });
            context.Properties.Add(new Property
            {
                Id = fixture.PropertyId,
                CompanyId = fixture.CompanyId,
                Name = "Test Property",
                AddressLine1 = "1 Test Street",
                City = "Nairobi",
                CountryCode = "KE",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            });
            context.Guests.Add(new Guest
            {
                Id = fixture.GuestId,
                CompanyId = fixture.CompanyId,
                FirstName = "Test",
                LastName = "Guest",
                Email = "guest@example.com",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            });
            await context.SaveChangesAsync();

            return fixture;
        }

        public ApplicationDbContext CreateContext() => new(_options);

        public async Task<Conversation> SeedConversationAsync()
        {
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                GuestId = GuestId,
                PropertyId = PropertyId,
                Channel = GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow
            };

            await using var context = CreateContext();
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync();
            return conversation;
        }

        public async Task<Reservation> SeedReservationAsync()
        {
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = PropertyId,
                PrimaryGuestId = GuestId,
                CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                Adults = 1,
                TotalGuestCount = 1,
                Status = ReservationStatus.Confirmed,
                IsActive = true
            };

            await using var context = CreateContext();
            context.Reservations.Add(reservation);
            await context.SaveChangesAsync();
            return reservation;
        }

        public async Task SeedHousekeepingRequestAsync(Guid conversationId)
        {
            await using var context = CreateContext();
            context.HousekeepingRequests.Add(new HousekeepingRequest
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = PropertyId,
                ReservationId = (await SeedReservationForRequestAsync(context)).Id,
                ConversationId = conversationId,
                RequestType = HousekeepingRequestType.RoomCleaning
            });
            await context.SaveChangesAsync();
        }

        public async Task SeedMaintenanceTicketAsync(Guid conversationId)
        {
            await using var context = CreateContext();
            context.MaintenanceTickets.Add(new MaintenanceTicket
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = PropertyId,
                ConversationId = conversationId,
                Category = MaintenanceCategory.Other,
                DescriptionSummary = "Test issue"
            });
            await context.SaveChangesAsync();
        }

        public async Task SeedConversationMessageFeedbackAsync(Guid conversationId)
        {
            await using var context = CreateContext();
            var message = await SeedConversationMessageAsync(context, conversationId);
            context.ConversationMessageFeedback.Add(new ConversationMessageFeedback
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ConversationId = conversationId,
                ConversationMessageId = message.Id,
                GuestId = GuestId,
                FeedbackValue = ConversationMessageFeedbackValue.Helpful
            });
            await context.SaveChangesAsync();
        }

        public async Task SeedConversationMessageKnowledgeSourceAsync(Guid conversationId)
        {
            await using var context = CreateContext();
            var message = await SeedConversationMessageAsync(context, conversationId);
            var article = new PropertyKnowledgeArticle
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = PropertyId,
                Title = "Test article",
                Content = "Test content",
                IsApproved = true,
                IsActive = true
            };
            context.PropertyKnowledgeArticles.Add(article);
            context.ConversationMessageKnowledgeSources.Add(new ConversationMessageKnowledgeSource
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ConversationId = conversationId,
                ConversationMessageId = message.Id,
                PropertyKnowledgeArticleId = article.Id,
                Rank = 1,
                IsPrimary = true
            });
            await context.SaveChangesAsync();
        }

        public async Task SeedPendingConciergeActionAsync(Guid conversationId)
        {
            await using var context = CreateContext();
            context.PendingConciergeActions.Add(new PendingConciergeAction
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ConversationId = conversationId,
                PropertyId = PropertyId,
                ActionType = ConciergeActionType.RequestEarlyCheckIn,
                SerializedNormalizedParameters = "{}",
                Status = PendingConciergeActionStatus.ReadyToExecute,
                IdempotencyKey = $"idem-{Guid.NewGuid():N}",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                CreatedFromMessageId = Guid.NewGuid()
            });
            await context.SaveChangesAsync();
        }

        private async Task<ConversationMessage> SeedConversationMessageAsync(ApplicationDbContext context, Guid conversationId)
        {
            var message = new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ConversationId = conversationId,
                SenderType = ConversationSenderType.Guest,
                Content = "Test message",
                SentAt = DateTimeOffset.UtcNow
            };
            context.ConversationMessages.Add(message);
            await context.SaveChangesAsync();
            return message;
        }

        public async Task<Guid> SeedAuditLogAsync(Guid conversationId)
        {
            var id = Guid.NewGuid();
            await using var context = CreateContext();
            context.ConciergeActionAuditLogs.Add(new ConciergeActionAuditLog
            {
                Id = id,
                CompanyId = CompanyId,
                ConversationId = conversationId,
                ActionType = ConciergeActionType.RequestEarlyCheckIn,
                EventType = ConciergeActionAuditEventType.Detected,
                ActorType = "Guest",
                Channel = "Web",
                ResultCode = "ok",
                CorrelationId = Guid.NewGuid().ToString("N")
            });
            await context.SaveChangesAsync();
            return id;
        }

        public async Task SeedGuestJourneyMessageAsync(Guid reservationId, Guid guestId)
        {
            var lifecycleEventId = Guid.NewGuid();
            await using var context = CreateContext();
            context.ReservationLifecycleEvents.Add(new ReservationLifecycleEvent
            {
                Id = lifecycleEventId,
                CompanyId = CompanyId,
                ReservationId = reservationId,
                PropertyId = PropertyId,
                GuestId = guestId,
                EventType = ReservationLifecycleEventType.PreArrival,
                RuleVersion = "v1",
                PropertyLocalDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ScheduledForUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"lifecycle-{Guid.NewGuid():N}"
            });
            context.GuestJourneyMessages.Add(new GuestJourneyMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ReservationId = reservationId,
                ReservationLifecycleEventId = lifecycleEventId,
                PropertyId = PropertyId,
                GuestId = guestId,
                JourneyEventType = ReservationLifecycleEventType.PreArrival,
                RenderedContent = "Hello",
                IdempotencyKey = $"journey-{Guid.NewGuid():N}"
            });
            await context.SaveChangesAsync();
        }

        public async Task SeedReservationLifecycleEventAsync(Guid reservationId, Guid guestId)
        {
            await using var context = CreateContext();
            context.ReservationLifecycleEvents.Add(new ReservationLifecycleEvent
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                ReservationId = reservationId,
                PropertyId = PropertyId,
                GuestId = guestId,
                EventType = ReservationLifecycleEventType.PreArrival,
                RuleVersion = "v1",
                PropertyLocalDate = DateOnly.FromDateTime(DateTime.UtcNow),
                ScheduledForUtc = DateTimeOffset.UtcNow,
                IdempotencyKey = $"lifecycle-{Guid.NewGuid():N}"
            });
            await context.SaveChangesAsync();
        }

        public async Task SoftDeleteConversationAsync(Guid conversationId)
        {
            await using var context = CreateContext();
            var conversation = await context.Conversations.IgnoreQueryFilters().SingleAsync(item => item.Id == conversationId);
            conversation.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        public async Task SoftDeleteGuestAsync(Guid guestId)
        {
            await using var context = CreateContext();
            var guest = await context.Guests.IgnoreQueryFilters().SingleAsync(item => item.Id == guestId);
            guest.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        public async Task SoftDeletePropertyAsync(Guid propertyId)
        {
            await using var context = CreateContext();
            var property = await context.Properties.IgnoreQueryFilters().SingleAsync(item => item.Id == propertyId);
            property.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        private async Task<Reservation> SeedReservationForRequestAsync(ApplicationDbContext context)
        {
            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = PropertyId,
                PrimaryGuestId = GuestId,
                CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
                Adults = 1,
                TotalGuestCount = 1,
                Status = ReservationStatus.Confirmed,
                IsActive = true
            };
            context.Reservations.Add(reservation);
            return reservation;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }
    }
}
