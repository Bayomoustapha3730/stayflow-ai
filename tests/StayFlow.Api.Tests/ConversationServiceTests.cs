using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task CreateOrGetConversationAsync_WithValidTenantGuest_CreatesConversation()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.CreateOrGetConversationAsync(new CreateConversationRequest
        {
            GuestId = fixture.Guest.Id,
            PropertyId = fixture.Property.Id,
            Channel = GuestChannel.Web
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(fixture.Repository.Conversations);
        Assert.Equal(ConversationStatus.Open, response.Data!.Status);
        Assert.Contains(fixture.Repository.AuditLogs, log => log.Action == "ConversationCreated");
    }

    [Fact]
    public async Task CreateOrGetConversationAsync_RejectsCrossTenantGuest()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.CreateOrGetConversationAsync(new CreateConversationRequest
        {
            GuestId = Guid.NewGuid(),
            Channel = GuestChannel.Web
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Guest was not found.", response.Message);
    }

    [Fact]
    public async Task CreateOrGetConversationAsync_BindsValidReservation()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.CreateOrGetConversationAsync(new CreateConversationRequest
        {
            GuestId = fixture.Guest.Id,
            ReservationId = fixture.Reservation.Id,
            Channel = GuestChannel.Email,
            ChannelIdentity = "guest@example.com"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(fixture.Reservation.Id, response.Data!.ReservationId);
        Assert.Equal(fixture.Property.Id, response.Data.PropertyId);
    }

    [Fact]
    public async Task CreateOrGetConversationAsync_RejectsReservationForAnotherGuest()
    {
        var fixture = new Fixture();
        fixture.Reservation.PrimaryGuestId = Guid.NewGuid();

        var response = await fixture.Service.CreateOrGetConversationAsync(new CreateConversationRequest
        {
            GuestId = fixture.Guest.Id,
            ReservationId = fixture.Reservation.Id,
            Channel = GuestChannel.Web
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation was not found.", response.Message);
    }

    [Fact]
    public async Task CreateOrGetConversationAsync_ReusesCompatibleOpenConversation()
    {
        var fixture = new Fixture();
        var existing = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(existing);

        var response = await fixture.Service.CreateOrGetConversationAsync(new CreateConversationRequest
        {
            GuestId = fixture.Guest.Id,
            Channel = existing.Channel,
            ChannelIdentity = existing.ChannelIdentity
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(existing.Id, response.Data!.Id);
        Assert.Single(fixture.Repository.Conversations);
    }

    [Fact]
    public async Task CreateOrGetConversationAsync_DoesNotReuseClosedConversation()
    {
        var fixture = new Fixture();
        fixture.Repository.Conversations.Add(fixture.Repository.NewConversation(status: ConversationStatus.Closed));

        var response = await fixture.Service.CreateOrGetConversationAsync(new CreateConversationRequest
        {
            GuestId = fixture.Guest.Id,
            Channel = GuestChannel.Web
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(2, fixture.Repository.Conversations.Count);
    }

    [Fact]
    public async Task AddGuestMessageAsync_StoresMessageAndUpdatesLastActivity()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        var previousActivity = conversation.LastActivityAt;

        var response = await fixture.Service.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest
        {
            Content = "Hello",
            SentAt = previousActivity.AddMinutes(5)
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(ConversationSenderType.Guest, response.Data!.SenderType);
        Assert.Equal(previousActivity.AddMinutes(5), conversation.LastActivityAt);
    }

    [Fact]
    public async Task AddInternalNoteAsync_IsExcludedFromGuestHistory()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        await fixture.Service.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest { Content = "Guest" }, CancellationToken.None);
        await fixture.Service.AddInternalNoteAsync(conversation.Id, new AddInternalNoteRequest { Content = "Host-only" }, CancellationToken.None);

        var response = await fixture.Service.GetConversationHistoryAsync(conversation.Id, new ConversationHistoryQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Messages.Items);
        Assert.DoesNotContain(response.Data.Messages.Items, message => message.Content == "Host-only");
    }

    [Fact]
    public async Task AddGuestMessageAsync_PreventsDuplicateExternalMessageId()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        var first = await fixture.Service.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest { Content = "One", ExternalMessageId = "ext-1" }, CancellationToken.None);
        var second = await fixture.Service.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest { Content = "Two", ExternalMessageId = "ext-1" }, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Single(fixture.Repository.Messages);
        Assert.Equal(first.Data!.Id, second.Data!.Id);
    }

    [Fact]
    public async Task AddGuestMessageAsync_RejectsOversizedMessage()
    {
        var fixture = new Fixture(maxMessageCharacters: 5);
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.Service.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest { Content = "Too long" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation message validation failed.", response.Message);
    }

    [Fact]
    public async Task AddAIMessageAsync_IsBlockedDuringHumanTakeover()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation(status: ConversationStatus.HumanManaged, humanTakeover: true);
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.Service.AddAIMessageAsync(conversation.Id, "AI", new DTOs.AIOrchestration.AIOrchestrationResult(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation state does not allow this message.", response.Message);
    }

    [Fact]
    public async Task CloseConversationAsync_BlocksNewMessages()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        await fixture.Service.CloseConversationAsync(conversation.Id, CancellationToken.None);
        var response = await fixture.Service.AddGuestMessageAsync(conversation.Id, new AddGuestMessageRequest { Content = "Hello again" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation state does not allow this message.", response.Message);
    }

    [Fact]
    public async Task GetConversationAsync_CrossTenantConversationReturnsNotFound()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation(overrideCompanyId: Guid.NewGuid());
        fixture.Repository.Conversations.Add(conversation);

        var response = await fixture.Service.GetConversationAsync(conversation.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation was not found.", response.Message);
    }

    private sealed class Fixture
    {
        public Fixture(int maxMessageCharacters = 2000)
        {
            Repository = new FakeConversationRepository(CompanyId);
            Guest = new Guest { Id = Guid.NewGuid(), CompanyId = CompanyId, FirstName = "Demo", LastName = "Guest", PreferredLanguage = "en", CountryCode = "KE", IsActive = true };
            Property = new Property { Id = Guid.NewGuid(), CompanyId = CompanyId, Name = "Demo", City = "Nairobi", CountryCode = "KE", AddressLine1 = "Road", TimeZone = "Africa/Nairobi", IsActive = true };
            Reservation = new Reservation { Id = Guid.NewGuid(), CompanyId = CompanyId, PropertyId = Property.Id, PrimaryGuestId = Guest.Id, Property = Property, PrimaryGuest = Guest, CheckInDate = new DateOnly(2026, 8, 1), CheckOutDate = new DateOnly(2026, 8, 4), IsActive = true };
            Repository.Guests.Add(Guest);
            Repository.Properties.Add(Property);
            Repository.Reservations.Add(Reservation);
            Service = new ConversationService(
                Repository,
                new FakeCurrentTenantContext(CompanyId),
                new ConversationStatusTransitionPolicy(),
                Options.Create(new ConversationOptions { MaxMessageCharacters = maxMessageCharacters, ReuseOpenConversationMinutes = 120, MaxHistoryMessages = 100 }));
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guest Guest { get; }
        public Property Property { get; }
        public Reservation Reservation { get; }
        public FakeConversationRepository Repository { get; }
        public ConversationService Service { get; }
    }

    private sealed class FakeCurrentTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "conversation-test";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class FakeConversationRepository(Guid companyId) : IConversationRepository
    {
        public List<Conversation> Conversations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];
        public List<Guest> Guests { get; } = [];
        public List<Property> Properties { get; } = [];
        public List<Reservation> Reservations { get; } = [];
        public List<User> Users { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<Conversation?> GetByIdForCompanyAsync(Guid requestedCompanyId, Guid conversationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Conversations.FirstOrDefault(conversation => conversation.CompanyId == requestedCompanyId && conversation.Id == conversationId));
        }

        public Task<Conversation?> GetOpenConversationAsync(Guid requestedCompanyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            return Task.FromResult(Conversations.FirstOrDefault(conversation =>
                conversation.CompanyId == requestedCompanyId
                && conversation.GuestId == guestId
                && conversation.Channel == channel
                && conversation.ChannelIdentity == channelIdentity
                && conversation.Status != ConversationStatus.Closed
                && conversation.LastActivityAt >= cutoff));
        }

        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid requestedCompanyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
        {
            var messages = Messages
                .Where(message => message.CompanyId == requestedCompanyId && message.ConversationId == conversationId)
                .Where(message => query.IncludeInternal || !message.IsInternal)
                .OrderBy(message => message.SentAt)
                .ToList();

            return Task.FromResult(new PagedResult<ConversationMessage>
            {
                Items = messages,
                PageNumber = 1,
                PageSize = messages.Count,
                TotalCount = messages.Count
            });
        }

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid requestedCompanyId, string externalMessageId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Messages.FirstOrDefault(message => message.CompanyId == requestedCompanyId && message.ExternalMessageId == externalMessageId));
        }

        public Task<Guest?> GetGuestAsync(Guid requestedCompanyId, Guid guestId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Guests.FirstOrDefault(guest => guest.CompanyId == requestedCompanyId && guest.Id == guestId && guest.IsActive));
        }

        public Task<Reservation?> GetReservationAsync(Guid requestedCompanyId, Guid reservationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Reservations.FirstOrDefault(reservation => reservation.CompanyId == requestedCompanyId && reservation.Id == reservationId && reservation.IsActive));
        }

        public Task<Property?> GetPropertyAsync(Guid requestedCompanyId, Guid propertyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Properties.FirstOrDefault(property => property.CompanyId == requestedCompanyId && property.Id == propertyId && property.IsActive));
        }

        public Task<User?> GetUserAsync(Guid requestedCompanyId, Guid userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Users.FirstOrDefault(user => user.CompanyId == requestedCompanyId && user.Id == userId && user.IsActive));
        }

        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
        {
            Conversations.Add(conversation);
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var conversation in Conversations.Where(conversation => conversation.CreatedAt == default))
            {
                conversation.CreatedAt = now;
                conversation.UpdatedAt = now;
            }

            foreach (var message in Messages.Where(message => message.CreatedAt == default))
            {
                message.CreatedAt = now;
                message.UpdatedAt = now;
            }

            return Task.CompletedTask;
        }

        public Conversation NewConversation(Guid? overrideCompanyId = null, ConversationStatus status = ConversationStatus.Open, bool humanTakeover = false)
        {
            var guest = Guests.Single();
            var property = Properties.Single();
            return new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = overrideCompanyId ?? companyId,
                GuestId = guest.Id,
                Guest = guest,
                PropertyId = property.Id,
                Property = property,
                Channel = GuestChannel.Web,
                ChannelIdentity = null,
                Status = status,
                HumanTakeoverEnabled = humanTakeover,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastActivityAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
