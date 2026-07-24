using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Tests;

public sealed class ConversationContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_ReturnsNull_WhenConversationIsInaccessible()
    {
        var fixture = new Fixture();
        var foreignConversation = fixture.Repository.NewConversation(overrideCompanyId: Guid.NewGuid());
        fixture.Repository.Conversations.Add(foreignConversation);

        var context = await fixture.Builder.BuildAsync(fixture.CompanyId, foreignConversation.Id, CancellationToken.None);

        Assert.Null(context);
    }

    [Fact]
    public async Task BuildAsync_IncludesPropertyAndReservationAndKnowledge()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        conversation.Reservation = fixture.Reservation;
        conversation.ReservationId = fixture.Reservation.Id;
        fixture.Repository.Conversations.Add(conversation);

        fixture.Property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            PropertyId = fixture.Property.Id,
            Title = "Wi-Fi Information",
            Content = "Network StayFlow, password 12345678",
            IsActive = true
        });

        var context = await fixture.Builder.BuildAsync(fixture.CompanyId, conversation.Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(fixture.Property.Id, context!.PropertyId);
        Assert.Equal(fixture.Reservation.Id, context.ReservationId);
        Assert.Single(context.ApprovedKnowledgeItems);
    }

    [Fact]
    public async Task BuildAsync_OrdersMessagesChronologicallyAndExcludesInternalNotes()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        fixture.Repository.Messages.AddRange(
        [
            fixture.Repository.NewMessage(conversation, "Second", ConversationSenderType.Guest, sentAt: DateTimeOffset.UtcNow.AddMinutes(2)),
            fixture.Repository.NewMessage(conversation, "Internal", ConversationSenderType.System, ConversationMessageType.InternalNote, isInternal: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(1)),
            fixture.Repository.NewMessage(conversation, "First", ConversationSenderType.Host, sentAt: DateTimeOffset.UtcNow)
        ]);

        var context = await fixture.Builder.BuildAsync(fixture.CompanyId, conversation.Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(["First", "Second"], context!.VisibleMessages.Select(message => message.Text));
    }

    [Fact]
    public async Task BuildAsync_ExcludesInactiveKnowledge()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        fixture.Property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            PropertyId = fixture.Property.Id,
            Title = "House Rules",
            Content = "No smoking",
            IsActive = true
        });
        fixture.Property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            PropertyId = fixture.Property.Id,
            Title = "Draft Note",
            Content = "Not approved",
            IsActive = false
        });

        var context = await fixture.Builder.BuildAsync(fixture.CompanyId, conversation.Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Single(context!.ApprovedKnowledgeItems);
        Assert.DoesNotContain(context.ApprovedKnowledgeItems, item => item.Title.Contains("Draft", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildAsync_EnforcesMessageAndKnowledgeLimitsAndMarksTruncation()
    {
        var fixture = new Fixture(new ConversationContextLimits
        {
            MaxVisibleMessages = 2,
            MaxMessageCharacters = 10,
            MaxTotalPromptContextCharacters = 50,
            MaxKnowledgeItems = 1,
            MaxKnowledgeItemCharacters = 10,
            ContextScanPageSize = 100
        });
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        fixture.Repository.Messages.AddRange(
        [
            fixture.Repository.NewMessage(conversation, "Guest first request with long text", ConversationSenderType.Guest, sentAt: DateTimeOffset.UtcNow),
            fixture.Repository.NewMessage(conversation, "Host response with long text", ConversationSenderType.Host, sentAt: DateTimeOffset.UtcNow.AddMinutes(1)),
            fixture.Repository.NewMessage(conversation, "Guest follow up with long text", ConversationSenderType.Guest, sentAt: DateTimeOffset.UtcNow.AddMinutes(2))
        ]);

        fixture.Property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            PropertyId = fixture.Property.Id,
            Title = "Wi-Fi",
            Content = "Very long wifi instructions",
            IsActive = true
        });
        fixture.Property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            PropertyId = fixture.Property.Id,
            Title = "Parking",
            Content = "Very long parking instructions",
            IsActive = true
        });

        var context = await fixture.Builder.BuildAsync(fixture.CompanyId, conversation.Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.True(context!.VisibleMessages.Count <= 2);
        Assert.True(context.ApprovedKnowledgeItems.Count <= 1);
        Assert.True(context.Truncated);
        Assert.Contains(ConversationContextWarning.ContextTruncated, context.Warnings);
    }

    [Fact]
    public async Task BuildAsync_NormalizesWhitespace()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);
        fixture.Repository.Messages.Add(fixture.Repository.NewMessage(conversation, "Hello\n\tworld   again", ConversationSenderType.Guest));

        var context = await fixture.Builder.BuildAsync(fixture.CompanyId, conversation.Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal("Hello world again", context!.VisibleMessages.Single().Text);
    }

    [Fact]
    public async Task BuildAsync_RespectsCancellationToken()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewConversation();
        fixture.Repository.Conversations.Add(conversation);

        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Builder.BuildAsync(fixture.CompanyId, conversation.Id, source.Token));
    }

    private sealed class Fixture
    {
        public Fixture(ConversationContextLimits? limits = null)
        {
            Repository = new FakeConversationRepository(CompanyId);
            Guest = new Guest
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                FirstName = "Demo",
                LastName = "Guest",
                Email = "demo.guest@stayflow.local",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            };
            Property = new Property
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                Name = "Demo Nairobi Apartment",
                City = "Nairobi",
                CountryCode = "KE",
                AddressLine1 = "Demo Street",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            };
            Reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = Property.Id,
                PrimaryGuestId = Guest.Id,
                ConfirmationNumber = "DEMO-CONF-001",
                CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
                Status = ReservationStatus.Confirmed,
                IsActive = true
            };

            Repository.Guests.Add(Guest);
            Repository.Properties.Add(Property);

            Builder = new ConversationContextBuilder(
                Repository,
                Options.Create(limits ?? new ConversationContextLimits()),
                NullLogger<ConversationContextBuilder>.Instance);
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guest Guest { get; }
        public Property Property { get; }
        public Reservation Reservation { get; }
        public FakeConversationRepository Repository { get; }
        public ConversationContextBuilder Builder { get; }
    }

    private sealed class FakeConversationRepository(Guid companyId) : IConversationRepository
    {
        public List<Conversation> Conversations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];
        public List<Guest> Guests { get; } = [];
        public List<Property> Properties { get; } = [];

        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid requestedCompanyId, ConversationListQueryParameters query, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Conversation?> GetByIdForCompanyAsync(Guid requestedCompanyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(Conversations.FirstOrDefault(conversation => conversation.CompanyId == requestedCompanyId && conversation.Id == conversationId));

        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid requestedCompanyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken)
        {
            var filtered = Messages
                .Where(message => message.CompanyId == requestedCompanyId && message.ConversationId == conversationId)
                .Where(message => query.IncludeInternal || !message.IsInternal)
                .OrderBy(message => message.SentAt)
                .ThenBy(message => message.CreatedAt)
                .Take(query.PageSize)
                .ToList();

            return Task.FromResult(new PagedResult<ConversationMessage>
            {
                Items = filtered,
                PageNumber = query.NormalizedPageNumber,
                PageSize = query.NormalizedPageSize,
                TotalCount = filtered.Count
            });
        }

        public Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<ConversationParticipantReadState?> GetReadStateAsync(Guid companyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid companyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Guest?> GetGuestAsync(Guid requestedCompanyId, Guid guestId, CancellationToken cancellationToken)
            => Task.FromResult(Guests.FirstOrDefault(guest => guest.CompanyId == requestedCompanyId && guest.Id == guestId));

        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<Property?> GetPropertyAsync(Guid requestedCompanyId, Guid propertyId, CancellationToken cancellationToken)
            => Task.FromResult(Properties.FirstOrDefault(property => property.CompanyId == requestedCompanyId && property.Id == propertyId));

        public Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Conversation NewConversation(Guid? overrideCompanyId = null)
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
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                LastActivityAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        public ConversationMessage NewMessage(
            Conversation conversation,
            string content,
            ConversationSenderType senderType,
            ConversationMessageType messageType = ConversationMessageType.Text,
            bool isInternal = false,
            DateTimeOffset? sentAt = null)
        {
            return new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = conversation.CompanyId,
                ConversationId = conversation.Id,
                Conversation = conversation,
                SenderType = senderType,
                MessageType = messageType,
                Content = content,
                IsInternal = isInternal,
                SentAt = sentAt ?? DateTimeOffset.UtcNow,
                CreatedAt = sentAt ?? DateTimeOffset.UtcNow,
                UpdatedAt = sentAt ?? DateTimeOffset.UtcNow
            };
        }
    }
}
