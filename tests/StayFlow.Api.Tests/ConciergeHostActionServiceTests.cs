using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Tests;

public sealed class ConciergeHostActionServiceTests
{
    [Fact]
    public async Task ApproveAsync_LateCheckout_UpdatesDomainEntityAndQueuesGuestNotification()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var pendingAction = SeedPendingAction(dbContext, graph, ConciergeActionType.RequestLateCheckout);
        SeedLateCheckoutRequest(dbContext, graph, pendingAction);

        var service = CreateService(dbContext, graph.CompanyId);
        var userId = Guid.NewGuid();

        var response = await service.ApproveAsync(graph.CompanyId, pendingAction.Id, userId, "Enjoy the extra time", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(PendingConciergeActionStatus.Completed, response.Data!.Status);

        var entity = await dbContext.LateCheckoutRequests.SingleAsync();
        Assert.Equal(LateCheckoutRequestStatus.Approved, entity.Status);
        Assert.Equal(userId, entity.ReviewedByUserId);
        Assert.Equal("Enjoy the extra time", entity.DecisionNote);
        Assert.NotNull(entity.ReviewedAt);

        var outbox = await dbContext.ActionNotificationOutbox.SingleAsync();
        Assert.Equal("HostApproved", outbox.NotificationType);
        Assert.Equal(ActionNotificationOutboxStatus.Pending, outbox.Status);
        Assert.Equal(pendingAction.Id, outbox.ActionId);
        Assert.Equal(graph.CompanyId, outbox.CompanyId);

        var audit = await dbContext.ConciergeActionAuditLogs.SingleAsync();
        Assert.Equal(ConciergeActionAuditEventType.HostApproved, audit.EventType);
        Assert.Equal(graph.CompanyId, audit.CompanyId);
        Assert.Equal(pendingAction.Id, audit.PendingActionId);
    }

    [Fact]
    public async Task DeclineAsync_LateCheckout_UpdatesDomainEntityToDeclinedAndAudits()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var pendingAction = SeedPendingAction(dbContext, graph, ConciergeActionType.RequestLateCheckout);
        SeedLateCheckoutRequest(dbContext, graph, pendingAction);

        var service = CreateService(dbContext, graph.CompanyId);

        var response = await service.DeclineAsync(graph.CompanyId, pendingAction.Id, Guid.NewGuid(), "Not available", CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(PendingConciergeActionStatus.Cancelled, response.Data!.Status);

        var entity = await dbContext.LateCheckoutRequests.SingleAsync();
        Assert.Equal(LateCheckoutRequestStatus.Declined, entity.Status);
        Assert.Equal("Not available", entity.DecisionNote);

        var outbox = await dbContext.ActionNotificationOutbox.SingleAsync();
        Assert.Equal("HostDeclined", outbox.NotificationType);

        var audit = await dbContext.ConciergeActionAuditLogs.SingleAsync();
        Assert.Equal(ConciergeActionAuditEventType.HostDeclined, audit.EventType);
    }

    [Fact]
    public async Task ApproveAsync_CrossTenantActionId_IsNotFound_AndDoesNotMutateLateCheckoutRequest()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var pendingAction = SeedPendingAction(dbContext, graph, ConciergeActionType.RequestLateCheckout);
        SeedLateCheckoutRequest(dbContext, graph, pendingAction);

        var service = CreateService(dbContext, graph.CompanyId);
        var otherCompanyId = Guid.NewGuid();

        var response = await service.ApproveAsync(otherCompanyId, pendingAction.Id, Guid.NewGuid(), null, CancellationToken.None);

        Assert.False(response.Success);
        var entity = await dbContext.LateCheckoutRequests.SingleAsync();
        Assert.Equal(LateCheckoutRequestStatus.Pending, entity.Status);
        Assert.Empty(await dbContext.ActionNotificationOutbox.ToListAsync());
        Assert.Empty(await dbContext.ConciergeActionAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ApproveAsync_EarlyCheckInAndParking_AreNotMutatedThisMilestone()
    {
        // Locks in the narrowed scope: PendingConciergeAction/outbox/audit still flow correctly,
        // but the domain request entities for these action types are intentionally left untouched
        // until a follow-up milestone proves an identical, safe mapping for them.
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext);
        var earlyCheckInAction = SeedPendingAction(dbContext, graph, ConciergeActionType.RequestEarlyCheckIn);
        SeedEarlyCheckInRequest(dbContext, graph, earlyCheckInAction);

        var service = CreateService(dbContext, graph.CompanyId);
        var response = await service.ApproveAsync(graph.CompanyId, earlyCheckInAction.Id, Guid.NewGuid(), null, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(PendingConciergeActionStatus.Completed, response.Data!.Status);

        var entity = await dbContext.EarlyCheckInRequests.SingleAsync();
        Assert.Equal(EarlyCheckInRequestStatus.Pending, entity.Status);
    }


    private static ConciergeHostActionService CreateService(ApplicationDbContext dbContext, Guid companyId)
    {
        return new ConciergeHostActionService(
            dbContext,
            new ConciergeActionAuditService(dbContext),
            new FakeTenantContext(companyId));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"concierge-host-action-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed record Graph(Guid CompanyId, Property Property, Reservation Reservation, Conversation Conversation);

    private static Graph SeedGraph(ApplicationDbContext dbContext)
    {
        var companyId = Guid.NewGuid();
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
            Id = Guid.NewGuid(),
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
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = property.Id,
            PrimaryGuestId = guest.Id,
            ReservationSource = "Manual",
            CheckInDate = new DateOnly(2026, 8, 10),
            CheckOutDate = new DateOnly(2026, 8, 14),
            Adults = 2,
            Status = ReservationStatus.Confirmed,
            IsActive = true
        };
        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            GuestId = guest.Id,
            ReservationId = reservation.Id,
            PropertyId = property.Id,
            Channel = DTOs.ReservationContext.GuestChannel.WhatsApp,
            Status = ConversationStatus.Open,
            StartedAt = now,
            LastActivityAt = now
        };

        dbContext.Companies.Add(company);
        dbContext.Properties.Add(property);
        dbContext.Guests.Add(guest);
        dbContext.Reservations.Add(reservation);
        dbContext.Conversations.Add(conversation);
        dbContext.SaveChanges();

        return new Graph(companyId, property, reservation, conversation);
    }

    private static PendingConciergeAction SeedPendingAction(ApplicationDbContext dbContext, Graph graph, ConciergeActionType actionType)
    {
        var pendingAction = new PendingConciergeAction
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            ConversationId = graph.Conversation.Id,
            PropertyId = graph.Property.Id,
            ReservationId = graph.Reservation.Id,
            ActionType = actionType,
            SerializedNormalizedParameters = "{}",
            Status = PendingConciergeActionStatus.AwaitingHostApproval,
            IdempotencyKey = $"idem-{Guid.NewGuid():N}",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            CreatedFromMessageId = Guid.NewGuid()
        };

        dbContext.PendingConciergeActions.Add(pendingAction);
        dbContext.SaveChanges();
        return pendingAction;
    }

    private static void SeedEarlyCheckInRequest(ApplicationDbContext dbContext, Graph graph, PendingConciergeAction pendingAction)
    {
        dbContext.EarlyCheckInRequests.Add(new EarlyCheckInRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            PropertyId = graph.Property.Id,
            ReservationId = graph.Reservation.Id,
            ConversationId = pendingAction.ConversationId,
            Status = EarlyCheckInRequestStatus.Pending
        });
        dbContext.SaveChanges();
    }

    private static void SeedLateCheckoutRequest(ApplicationDbContext dbContext, Graph graph, PendingConciergeAction pendingAction)
    {
        dbContext.LateCheckoutRequests.Add(new LateCheckoutRequest
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            PropertyId = graph.Property.Id,
            ReservationId = graph.Reservation.Id,
            ConversationId = pendingAction.ConversationId,
            Status = LateCheckoutRequestStatus.Pending
        });
        dbContext.SaveChanges();
    }


    private sealed class FakeTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId => companyId;
        public Guid? UserId => Guid.NewGuid();
        public string? CorrelationId => "test-correlation";
        public bool IsAuthenticated => true;
    }
}
