using Microsoft.Extensions.Options;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Controllers;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Extensions;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.Equal(existing.Id, response.Data!.ConversationId);
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

    [Fact]
    public async Task GetConversationsAsync_ReturnsTenantScopedConversationsOnly()
    {
        var fixture = new Fixture();
        var tenantConversation = fixture.Repository.NewInboxConversation(subject: "Tenant conversation");
        var crossTenantConversation = fixture.Repository.NewInboxConversation(overrideCompanyId: Guid.NewGuid(), subject: "Other tenant");
        fixture.Repository.InboxConversations.AddRange([tenantConversation, crossTenantConversation]);

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
        Assert.Equal(tenantConversation.ConversationId, response.Data.Items.Single().ConversationId);
        Assert.Equal(fixture.CompanyId, fixture.Repository.LastCompanyId);
    }

    [Fact]
    public async Task GetConversationsAsync_OrdersHostAttentionConversationsBeforeOpenConversations()
    {
        var fixture = new Fixture();
        var oldAttention = fixture.Repository.NewInboxConversation(status: ConversationStatus.Escalated, lastActivityAt: DateTimeOffset.UtcNow.AddHours(-2));
        var newestOpen = fixture.Repository.NewInboxConversation(status: ConversationStatus.Open, lastActivityAt: DateTimeOffset.UtcNow);
        var newerAttention = fixture.Repository.NewInboxConversation(status: ConversationStatus.HumanManaged, lastActivityAt: DateTimeOffset.UtcNow.AddHours(-1));
        fixture.Repository.InboxConversations.AddRange([oldAttention, newestOpen, newerAttention]);

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(
            [newerAttention.ConversationId, oldAttention.ConversationId, newestOpen.ConversationId],
            response.Data!.Items.Select(item => item.ConversationId).ToList());
    }

    [Fact]
    public async Task GetConversationsAsync_FiltersByStatus()
    {
        var fixture = new Fixture();
        var escalated = fixture.Repository.NewInboxConversation(status: ConversationStatus.Escalated);
        fixture.Repository.InboxConversations.AddRange([fixture.Repository.NewInboxConversation(status: ConversationStatus.Open), escalated]);

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters { Status = ConversationStatus.Escalated }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
        Assert.Equal(escalated.ConversationId, response.Data.Items.Single().ConversationId);
    }

    [Fact]
    public async Task GetConversationsAsync_FiltersByProperty()
    {
        var fixture = new Fixture();
        var propertyId = Guid.NewGuid();
        var target = fixture.Repository.NewInboxConversation(propertyId: propertyId);
        fixture.Repository.InboxConversations.AddRange([fixture.Repository.NewInboxConversation(), target]);

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters { PropertyId = propertyId }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
        Assert.Equal(target.ConversationId, response.Data.Items.Single().ConversationId);
    }

    [Fact]
    public async Task GetConversationsAsync_FiltersByRequiresHostAttention()
    {
        var fixture = new Fixture();
        var attention = fixture.Repository.NewInboxConversation(status: ConversationStatus.AwaitingHost);
        fixture.Repository.InboxConversations.AddRange([fixture.Repository.NewInboxConversation(status: ConversationStatus.Open), attention]);

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters { RequiresHostAttention = true }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
        Assert.Equal(attention.ConversationId, response.Data.Items.Single().ConversationId);
        Assert.True(response.Data.Items.Single().RequiresHostAttention);
    }

    [Theory]
    [InlineData("demo guest")]
    [InlineData("demo.guest@example.com")]
    [InlineData("Westlands")]
    [InlineData("CONF-001")]
    public async Task GetConversationsAsync_SearchesSupportedFields(string search)
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewInboxConversation(
            guestFirstName: "Demo",
            guestLastName: "Guest",
            guestEmail: "demo.guest@example.com",
            propertyName: "Westlands Apartment",
            confirmationNumber: "CONF-001",
            subject: "Arrival help");
        fixture.Repository.InboxConversations.Add(conversation);

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters { Search = $"  {search}  " }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Items);
        Assert.Equal(conversation.ConversationId, response.Data.Items.Single().ConversationId);
        Assert.Equal(search.Trim(), fixture.Repository.LastQuery!.Search);
    }

    [Fact]
    public async Task GetConversationsAsync_InternalNotesAreExcludedFromPreviewAndVisibleCount()
    {
        var fixture = new Fixture();
        var conversation = fixture.Repository.NewInboxConversation(
            latestVisibleMessagePreview: "Visible guest message",
            latestVisibleMessageSenderType: ConversationSenderType.Guest,
            totalVisibleMessageCount: 1);
        fixture.Repository.InboxConversations.Add(conversation);

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        var item = response.Data!.Items.Single();
        Assert.Equal("Visible guest message", item.LatestVisibleMessagePreview);
        Assert.Equal(ConversationSenderType.Guest, item.LatestVisibleMessageSenderType);
        Assert.Equal(1, item.TotalVisibleMessageCount);
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsPaginationMetadata()
    {
        var fixture = new Fixture();
        fixture.Repository.InboxConversations.AddRange(Enumerable.Range(0, 3).Select(index => fixture.Repository.NewInboxConversation(subject: $"Conversation {index}")));

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters { Page = 2, PageSize = 2 }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(3, response.Data!.TotalCount);
        Assert.Equal(2, response.Data.Page);
        Assert.Equal(2, response.Data.PageSize);
        Assert.Equal(2, response.Data.TotalPages);
        Assert.Single(response.Data.Items);
    }

    [Theory]
    [InlineData(0, 25, "Page must be greater than or equal to 1.")]
    [InlineData(1, 101, "PageSize must be 100 or fewer.")]
    public async Task GetConversationsAsync_RejectsInvalidPagination(int page, int pageSize, string expectedError)
    {
        var fixture = new Fixture();

        var response = await fixture.Service.GetConversationsAsync(new ConversationListQueryParameters { Page = page, PageSize = pageSize }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Conversation list query validation failed.", response.Message);
        Assert.Contains(expectedError, response.Errors);
    }

    [Fact]
    public void GetConversations_RequiresConversationsReadPermission()
    {
        var method = typeof(ConversationsController).GetMethod(nameof(ConversationsController.GetConversations));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequiresPermissionAttribute), inherit: false).Cast<RequiresPermissionAttribute>());

        Assert.Equal("conversations.read", attribute.Permission);
    }

    [Fact]
    public void AddApplicationServices_RegistersConversationInboxDependencies()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddApplicationServices(configuration);

        Assert.Contains(services, service => service.ServiceType == typeof(IConversationRepository) && service.ImplementationType == typeof(ConversationRepository));
        Assert.Contains(services, service => service.ServiceType == typeof(IConversationService) && service.ImplementationType == typeof(ConversationService));
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
        public List<FakeConversation> InboxConversations { get; } = [];
        public Guid? LastCompanyId { get; private set; }
        public ConversationListQueryParameters? LastQuery { get; private set; }

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

        public Task<PagedResult<ConversationSummaryResponse>> GetInboxAsync(
            Guid requestedCompanyId,
            ConversationListQueryParameters query,
            CancellationToken cancellationToken)
        {
            LastCompanyId = requestedCompanyId;
            LastQuery = query;
            var conversations = InboxConversations
                .Where(conversation => conversation.CompanyId == requestedCompanyId);

            if (query.Status is { } status)
            {
                conversations = conversations.Where(conversation => conversation.Status == status);
            }

            if (query.PropertyId is { } propertyId)
            {
                conversations = conversations.Where(conversation => conversation.Property!.PropertyId == propertyId);
            }

            if (query.RequiresHostAttention is { } requiresHostAttention)
            {
                conversations = conversations.Where(conversation => conversation.RequiresHostAttention == requiresHostAttention);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                conversations = conversations.Where(conversation =>
                    $"{conversation.Guest!.FirstName} {conversation.Guest.LastName}".Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (conversation.Guest.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || conversation.Property!.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (conversation.Reservation?.ConfirmationNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (conversation.Subject?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var ordered = conversations
                .OrderByDescending(conversation => conversation.RequiresHostAttention)
                .ThenByDescending(conversation => conversation.LastActivityAt)
                .ToList();
            var pageSize = query.NormalizedPageSize;
            var items = ordered
                .Skip((query.Page - 1) * pageSize)
                .Take(pageSize)
                .Select(conversation => conversation.ToResponse())
                .ToList();

            return Task.FromResult(new PagedResult<ConversationSummaryResponse>
            {
                Items = items,
                PageNumber = query.Page,
                PageSize = pageSize,
                TotalCount = ordered.Count
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

        public FakeConversation NewInboxConversation(
            Guid? overrideCompanyId = null,
            Guid? propertyId = null,
            ConversationStatus status = ConversationStatus.Open,
            DateTimeOffset? lastActivityAt = null,
            string? subject = null,
            string guestFirstName = "Demo",
            string guestLastName = "Guest",
            string? guestEmail = null,
            string propertyName = "Demo Property",
            string? confirmationNumber = null,
            string? latestVisibleMessagePreview = null,
            ConversationSenderType? latestVisibleMessageSenderType = null,
            int totalVisibleMessageCount = 0)
        {
            var resolvedPropertyId = propertyId ?? Guid.NewGuid();
            return new FakeConversation
            {
                CompanyId = overrideCompanyId ?? companyId,
                ConversationId = Guid.NewGuid(),
                Status = status,
                Channel = "WhatsApp",
                Subject = subject,
                Guest = new ConversationGuestSummary
                {
                    GuestId = Guid.NewGuid(),
                    FirstName = guestFirstName,
                    LastName = guestLastName,
                    Email = guestEmail
                },
                Property = new ConversationPropertySummary
                {
                    PropertyId = resolvedPropertyId,
                    Name = propertyName
                },
                Reservation = confirmationNumber is null
                    ? null
                    : new ConversationReservationSummary
                    {
                        ReservationId = Guid.NewGuid(),
                        ConfirmationNumber = confirmationNumber
                    },
                StartedAt = DateTimeOffset.UtcNow.AddHours(-3),
                LastActivityAt = lastActivityAt ?? DateTimeOffset.UtcNow,
                LatestVisibleMessagePreview = latestVisibleMessagePreview,
                LatestVisibleMessageSenderType = latestVisibleMessageSenderType,
                TotalVisibleMessageCount = totalVisibleMessageCount
            };
        }
    }

    private sealed class FakeConversation
    {
        public Guid CompanyId { get; init; }
        public Guid ConversationId { get; init; }
        public ConversationStatus Status { get; init; }
        public string Channel { get; init; } = string.Empty;
        public string? Subject { get; init; }
        public ConversationGuestSummary? Guest { get; init; }
        public ConversationPropertySummary? Property { get; init; }
        public ConversationReservationSummary? Reservation { get; init; }
        public DateTimeOffset StartedAt { get; init; }
        public DateTimeOffset LastActivityAt { get; init; }
        public string? LatestVisibleMessagePreview { get; init; }
        public ConversationSenderType? LatestVisibleMessageSenderType { get; init; }
        public int TotalVisibleMessageCount { get; init; }
        public bool RequiresHostAttention => Status is ConversationStatus.AwaitingHost or ConversationStatus.Escalated or ConversationStatus.HumanManaged;

        public ConversationSummaryResponse ToResponse()
        {
            return new ConversationSummaryResponse
            {
                ConversationId = ConversationId,
                Status = Status,
                Channel = Channel,
                Subject = Subject,
                Guest = Guest,
                Property = Property,
                Reservation = Reservation,
                AssignedUser = null,
                HumanTakeoverEnabled = RequiresHostAttention,
                RequiresHostAttention = RequiresHostAttention,
                EscalationReason = null,
                StartedAt = StartedAt,
                LastActivityAt = LastActivityAt,
                ClosedAt = Status == ConversationStatus.Closed ? LastActivityAt : null,
                LatestVisibleMessagePreview = LatestVisibleMessagePreview,
                LatestVisibleMessageSenderType = LatestVisibleMessageSenderType,
                LatestVisibleMessageTimestamp = LatestVisibleMessagePreview is null ? null : LastActivityAt,
                TotalVisibleMessageCount = TotalVisibleMessageCount
            };
        }
    }
}
