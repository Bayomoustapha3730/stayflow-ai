using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationLifecycleGuestJourneyHandlerTests
{
    [Theory]
    [InlineData(ReservationLifecycleEventType.PreArrival)]
    [InlineData(ReservationLifecycleEventType.ArrivalDay)]
    [InlineData(ReservationLifecycleEventType.InStay)]
    [InlineData(ReservationLifecycleEventType.CheckoutDay)]
    [InlineData(ReservationLifecycleEventType.PostStay)]
    public async Task HandleAsync_EligibleEvent_CreatesExactlyOneGuestJourneyMessage(ReservationLifecycleEventType eventType)
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var lifecycleEvent = SeedLifecycleEvent(dbContext, graph, eventType);
        var conversationService = new FakeConversationService(dbContext);
        var handler = CreateHandler(dbContext, conversationService);

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        var message = await dbContext.GuestJourneyMessages.SingleAsync(item => item.ReservationLifecycleEventId == lifecycleEvent.Id);
        Assert.Equal(eventType, message.JourneyEventType);
        Assert.Equal(GuestJourneyMessageStatus.Pending, message.Status);
        Assert.Equal(graph.CompanyId, message.CompanyId);
        Assert.Equal(graph.Reservation.Id, message.ReservationId);
        Assert.Equal(graph.Property.Id, message.PropertyId);
        Assert.Equal(graph.Guest.Id, message.GuestId);
        Assert.NotNull(message.ConversationId);
        Assert.False(string.IsNullOrWhiteSpace(message.RenderedContent));
        Assert.Equal(1, conversationService.CreateOrGetCallCount);
        Assert.False(conversationService.AddLifecycleAutomationMessageAsyncCalled);
    }

    [Fact]
    public async Task HandleAsync_CalledTwiceForSameEvent_DoesNotDuplicate()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var lifecycleEvent = SeedLifecycleEvent(dbContext, graph, ReservationLifecycleEventType.ArrivalDay);
        var conversationService = new FakeConversationService(dbContext);
        var handler = CreateHandler(dbContext, conversationService);

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);
        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        Assert.Equal(1, await dbContext.GuestJourneyMessages.CountAsync(item => item.ReservationLifecycleEventId == lifecycleEvent.Id));
    }

    [Fact]
    public async Task HandleAsync_SecondLifecycleEventForSameReservation_ReusesExistingConversation()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var conversationService = new FakeConversationService(dbContext);
        var handler = CreateHandler(dbContext, conversationService);

        var first = SeedLifecycleEvent(dbContext, graph, ReservationLifecycleEventType.PreArrival);
        await handler.HandleAsync(first, CancellationToken.None);
        Assert.Equal(1, conversationService.CreateOrGetCallCount);

        var second = SeedLifecycleEvent(dbContext, graph, ReservationLifecycleEventType.ArrivalDay);
        await handler.HandleAsync(second, CancellationToken.None);

        // Reused via GetLatestConversationForReservationAsync; conversation creation is not invoked again.
        Assert.Equal(1, conversationService.CreateOrGetCallCount);

        var messages = await dbContext.GuestJourneyMessages.Where(item => item.ReservationId == graph.Reservation.Id).ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Equal(messages[0].ConversationId, messages[1].ConversationId);
    }

    [Fact]
    public async Task HandleAsync_SameGuestDifferentReservations_CreatesIndependentMessages()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var secondReservation = NewReservation(graph.CompanyId, graph.Property.Id, graph.Guest.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 5));
        dbContext.Reservations.Add(secondReservation);
        await dbContext.SaveChangesAsync();

        var conversationService = new FakeConversationService(dbContext);
        var handler = CreateHandler(dbContext, conversationService);

        var firstEvent = SeedLifecycleEvent(dbContext, graph, ReservationLifecycleEventType.PreArrival);
        var secondEvent = SeedLifecycleEvent(dbContext, graph, ReservationLifecycleEventType.PreArrival, reservation: secondReservation);

        await handler.HandleAsync(firstEvent, CancellationToken.None);
        await handler.HandleAsync(secondEvent, CancellationToken.None);

        var firstMessage = await dbContext.GuestJourneyMessages.SingleAsync(item => item.ReservationLifecycleEventId == firstEvent.Id);
        var secondMessage = await dbContext.GuestJourneyMessages.SingleAsync(item => item.ReservationLifecycleEventId == secondEvent.Id);
        Assert.NotEqual(firstMessage.ReservationId, secondMessage.ReservationId);
        Assert.Equal(firstMessage.GuestId, secondMessage.GuestId);
    }

    [Fact]
    public async Task HandleAsync_DifferentCompanies_RemainIsolated()
    {
        await using var dbContext = CreateDbContext();
        var companyA = SeedGraph(dbContext);
        var companyB = SeedGraph(dbContext);
        var conversationService = new FakeConversationService(dbContext);
        var handler = CreateHandler(dbContext, conversationService);

        var eventA = SeedLifecycleEvent(dbContext, companyA, ReservationLifecycleEventType.ArrivalDay);
        var eventB = SeedLifecycleEvent(dbContext, companyB, ReservationLifecycleEventType.ArrivalDay);

        await handler.HandleAsync(eventA, CancellationToken.None);
        await handler.HandleAsync(eventB, CancellationToken.None);

        Assert.Equal(1, await dbContext.GuestJourneyMessages.CountAsync(item => item.CompanyId == companyA.CompanyId));
        Assert.Equal(1, await dbContext.GuestJourneyMessages.CountAsync(item => item.CompanyId == companyB.CompanyId));
    }

    [Fact]
    public async Task HandleAsync_WrongGuestIdentity_CreatesNoMessage()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var lifecycleEvent = SeedLifecycleEvent(dbContext, graph, ReservationLifecycleEventType.ArrivalDay);

        // Simulate the reservation's guest changing after the lifecycle event was scheduled.
        graph.Reservation.PrimaryGuestId = Guid.NewGuid();
        await dbContext.SaveChangesAsync();

        var conversationService = new FakeConversationService(dbContext);
        var handler = CreateHandler(dbContext, conversationService);

        await handler.HandleAsync(lifecycleEvent, CancellationToken.None);

        Assert.Empty(dbContext.GuestJourneyMessages);
        Assert.Equal(0, conversationService.CreateOrGetCallCount);
    }

    private static ReservationLifecycleGuestJourneyHandler CreateHandler(ApplicationDbContext dbContext, FakeConversationService conversationService)
    {
        return new ReservationLifecycleGuestJourneyHandler(
            new GuestJourneyMessageRepository(dbContext),
            new GuestJourneyMessageService(new GuestJourneyMessageRepository(dbContext)),
            new ReservationLifecycleMessageComposer(),
            conversationService,
            new TenantExecutionContextAccessor(),
            NullLogger<ReservationLifecycleGuestJourneyHandler>.Instance);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"guest-journey-handler-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed record ReservationGraph(Guid CompanyId, Company Company, Property Property, Guest Guest, Reservation Reservation);

    private static ReservationGraph SeedGraph(ApplicationDbContext dbContext)
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var suffix = companyId.ToString("N")[..8];
        var company = new Company
        {
            Id = companyId,
            Name = $"Company {suffix}",
            Slug = $"company-{suffix}",
            NormalizedSlug = $"COMPANY-{suffix}".ToUpperInvariant(),
            Status = "Active",
            Email = $"{suffix}@example.com",
            PhoneNumber = "+254700000001",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        };
        var property = new Property
        {
            Id = propertyId,
            CompanyId = companyId,
            Name = "Demo Property",
            AddressLine1 = "Road",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        };
        var guest = new Guest
        {
            Id = guestId,
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };
        var reservation = NewReservation(companyId, propertyId, guestId, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14));

        dbContext.Companies.Add(company);
        dbContext.Properties.Add(property);
        dbContext.Guests.Add(guest);
        dbContext.Reservations.Add(reservation);
        dbContext.SaveChanges();

        return new ReservationGraph(companyId, company, property, guest, reservation);
    }

    private static Reservation NewReservation(Guid companyId, Guid propertyId, Guid guestId, DateOnly checkInDate, DateOnly checkOutDate)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ReservationSource = "Manual",
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            Adults = 2,
            Status = ReservationStatus.Confirmed,
            IsActive = true
        };
    }

    private static ReservationLifecycleEvent SeedLifecycleEvent(
        ApplicationDbContext dbContext,
        ReservationGraph graph,
        ReservationLifecycleEventType eventType,
        Reservation? reservation = null)
    {
        var targetReservation = reservation ?? graph.Reservation;
        var propertyLocalDate = targetReservation.CheckInDate;
        var lifecycleEvent = new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            ReservationId = targetReservation.Id,
            PropertyId = graph.Property.Id,
            GuestId = targetReservation.PrimaryGuestId,
            EventType = eventType,
            RuleVersion = ReservationLifecycleRuleVersions.V1,
            PropertyLocalDate = propertyLocalDate,
            ScheduledForUtc = new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero),
            Status = ReservationLifecycleEventStatus.Processing,
            IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(graph.CompanyId, targetReservation.Id, eventType, propertyLocalDate, ReservationLifecycleRuleVersions.V1)
        };
        dbContext.ReservationLifecycleEvents.Add(lifecycleEvent);
        dbContext.SaveChanges();
        return lifecycleEvent;
    }

    private sealed class FakeConversationService(ApplicationDbContext dbContext) : IConversationService
    {
        public int CreateOrGetCallCount { get; private set; }
        public bool AddLifecycleAutomationMessageAsyncCalled { get; private set; }

        // Persists a Conversation row like the real ConversationService does, so the handler's
        // GetLatestConversationForReservationAsync reuse path is exercised faithfully.
        public async Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken)
        {
            CreateOrGetCallCount++;

            var reservation = await dbContext.Reservations.SingleAsync(item => item.Id == request.ReservationId, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = reservation.CompanyId,
                GuestId = request.GuestId,
                ReservationId = request.ReservationId,
                PropertyId = request.PropertyId,
                Channel = request.Channel,
                Status = ConversationStatus.Open,
                Subject = request.Subject,
                StartedAt = now,
                LastActivityAt = now
            };

            dbContext.Conversations.Add(conversation);
            await dbContext.SaveChangesAsync(cancellationToken);

            return ApiResponse<ConversationDetailResponse>.Ok(new ConversationDetailResponse
            {
                Id = conversation.Id,
                ConversationId = conversation.Id,
                GuestId = request.GuestId,
                ReservationId = request.ReservationId,
                PropertyId = request.PropertyId,
                Guest = null!,
                Reservation = null,
                Property = null,
                AssignedUser = null
            });
        }

        public Task<ApiResponse<ConversationMessageResponse>> AddLifecycleAutomationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken)
        {
            AddLifecycleAutomationMessageAsyncCalled = true;
            throw new InvalidOperationException("The lifecycle handler must never call AddLifecycleAutomationMessageAsync.");
        }

        public Task<ApiResponse<ConversationListResponse>> GetConversationsAsync(ConversationListQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationHistoryResponse>> GetConversationHistoryAsync(Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddGuestMessageAsync(Guid conversationId, AddGuestMessageRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddAIMessageAsync(Guid conversationId, string content, AIOrchestrationResult result, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddHostMessageAsync(Guid conversationId, AddHostMessageRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> RetryFailedMessageAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddPaymentConfirmationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> UpdateMessageDeliveryStatusAsync(Guid conversationId, Guid messageId, ConversationMessageDeliveryStatus status, DateTimeOffset occurredAt, string? failureCode, string? failureReason, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> EscalateConversationAsync(Guid conversationId, EscalateConversationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> EnableHumanTakeoverAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> ReturnToAIModeAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> ResolveConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> CloseConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> AssignConversationToCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> UnassignConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<bool>> MarkConversationReadForCurrentUserAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<bool>> MarkConversationReadForGuestAsync(Guid conversationId, Guid guestId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ChatMessageFeedbackResponse>> AddGuestMessageFeedbackAsync(Guid conversationId, Guid messageId, AddChatMessageFeedbackRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationFeedbackAnalyticsResponse>> GetFeedbackAnalyticsAsync(ConversationFeedbackAnalyticsQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
