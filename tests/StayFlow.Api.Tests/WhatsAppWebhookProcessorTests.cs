using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppWebhookProcessorTests
{
    [Fact]
    public async Task ProcessAsync_TextMessageWithSingleReservation_ReusesChatPipeline()
    {
        var fixture = new Fixture();
        fixture.Repository.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            PropertyId = fixture.PropertyId,
            PrimaryGuestId = fixture.Guest.Id,
            Property = new Property { Id = fixture.PropertyId, CompanyId = fixture.CompanyId, Name = "Demo", City = "Nairobi", CountryCode = "KE", AddressLine1 = "Road", TimeZone = "Africa/Nairobi", IsActive = true },
            CheckInDate = new DateOnly(2026, 7, 20),
            CheckOutDate = new DateOnly(2026, 7, 28),
            Status = ReservationStatus.CheckedIn,
            IsActive = true
        });

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.1", "+14155551234"), "cid-1", CancellationToken.None);

        Assert.NotNull(fixture.ChatService.Request);
        Assert.Equal(GuestChannel.WhatsApp, fixture.ChatService.Request!.Channel);
        Assert.Equal("wamid.1", fixture.ChatService.Request.ExternalMessageId);
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
    }

    [Fact]
    public async Task ProcessAsync_AmbiguousReservations_RoutesToHostAttentionFlow()
    {
        var fixture = new Fixture();
        fixture.Repository.Reservations.AddRange(
            CreateReservation(fixture.CompanyId, fixture.PropertyId, fixture.Guest.Id, new DateOnly(2026, 7, 20), new DateOnly(2026, 7, 28), ReservationStatus.CheckedIn),
            CreateReservation(fixture.CompanyId, fixture.PropertyId, fixture.Guest.Id, new DateOnly(2026, 7, 21), new DateOnly(2026, 7, 29), ReservationStatus.ActiveStay));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.2", "+14155551234"), "cid-2", CancellationToken.None);

        Assert.Null(fixture.ChatService.Request);
        Assert.NotNull(fixture.ConversationService.CreatedConversationRequest);
        Assert.True(fixture.ConversationService.HumanTakeoverEnabled);
        Assert.Equal(ConversationMessageProvider.WhatsAppCloud, fixture.ConversationService.LastGuestMessageRequest!.Provider);
    }

    [Fact]
    public async Task ProcessAsync_StatusEvent_UpdatesOutboundDeliveryStatus()
    {
        var fixture = new Fixture();
        fixture.Repository.Messages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            ConversationId = fixture.ConversationId,
            Provider = ConversationMessageProvider.WhatsAppCloud,
            ExternalMessageId = "wamid.outbound",
            SenderType = ConversationSenderType.Host,
            MessageType = ConversationMessageType.Text,
            Content = "Reply",
            SentAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await fixture.Processor.ProcessAsync(BuildStatusPayload("wamid.outbound", "read"), "cid-3", CancellationToken.None);

        Assert.Equal(ConversationMessageDeliveryStatus.Read, fixture.ConversationService.LastDeliveryStatus);
    }

    [Fact]
    public async Task FakeConversationService_RetryFailedMessageAsync_RecordsIdentifiers()
    {
        var service = new FakeConversationService(Guid.NewGuid());
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var result = await service.RetryFailedMessageAsync(conversationId, messageId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(conversationId, service.RetriedConversationId);
        Assert.Equal(messageId, service.RetriedMessageId);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateProviderMessageId_DoesNotReprocessTheGuestMessage()
    {
        var fixture = new Fixture();
        fixture.Repository.Reservations.Add(CreateReservation(
            fixture.CompanyId,
            fixture.PropertyId,
            fixture.Guest.Id,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 28),
            ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.dedupe", "+14155551234"), "cid-dupe", CancellationToken.None);
        Assert.Equal(1, fixture.ChatService.SendCallCount);

        // The provider redelivers the same message id; the persisted message must suppress reprocessing.
        fixture.Repository.Messages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            ConversationId = fixture.ConversationId,
            Provider = ConversationMessageProvider.WhatsAppCloud,
            ExternalMessageId = "wamid.dedupe",
            SenderType = ConversationSenderType.Guest,
            MessageType = ConversationMessageType.Text,
            Content = "What time is check-in?",
            SentAt = DateTimeOffset.UtcNow
        });

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.dedupe", "+14155551234"), "cid-dupe", CancellationToken.None);

        Assert.Equal(1, fixture.ChatService.SendCallCount);
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
    }

    [Fact]
    public async Task ProcessAsync_DerivesTenantFromIntegrationDuringBackgroundProcessing()
    {
        var fixture = new Fixture();
        fixture.Repository.Reservations.Add(CreateReservation(
            fixture.CompanyId,
            fixture.PropertyId,
            fixture.Guest.Id,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 28),
            ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.tenant", "+14155551234"), "cid-tenant", CancellationToken.None);

        Assert.Equal([fixture.CompanyId], fixture.ChatService.ObservedTenantCompanyIds);
        Assert.Null(fixture.TenantAccessor.CompanyId);
    }

    [Fact]
    public async Task ProcessAsync_UnknownPhoneNumberId_IsIgnoredWithoutTouchingAnyTenant()
    {
        var fixture = new Fixture();
        fixture.Repository.Integration = null;

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.unknown", "+14155551234"), "cid-unknown", CancellationToken.None);

        Assert.Equal(0, fixture.ChatService.SendCallCount);
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
        Assert.Null(fixture.TenantAccessor.CompanyId);
    }

    [Fact]
    public async Task ProcessAsync_StatusEventForUnknownProviderMessage_DoesNotMarkAnyMessageDelivered()
    {
        var fixture = new Fixture();

        await fixture.Processor.ProcessAsync(BuildStatusPayload("wamid.missing", "delivered"), "cid-missing", CancellationToken.None);

        Assert.Null(fixture.ConversationService.LastDeliveryStatus);
    }

    [Fact]
    public async Task ProcessAsync_FailedStatusEvent_RecordsFailureInsteadOfDelivery()
    {
        var fixture = new Fixture();
        fixture.Repository.Messages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            ConversationId = fixture.ConversationId,
            Provider = ConversationMessageProvider.WhatsAppCloud,
            ExternalMessageId = "wamid.failed",
            SenderType = ConversationSenderType.AI,
            MessageType = ConversationMessageType.Text,
            Content = "Check-in is at 3:00 PM.",
            SentAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await fixture.Processor.ProcessAsync(BuildStatusPayload("wamid.failed", "failed"), "cid-failed", CancellationToken.None);

        Assert.Equal(ConversationMessageDeliveryStatus.Failed, fixture.ConversationService.LastDeliveryStatus);
    }

    private static Reservation CreateReservation(Guid companyId, Guid propertyId, Guid guestId, DateOnly checkIn, DateOnly checkOut, ReservationStatus status)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            Property = new Property { Id = propertyId, CompanyId = companyId, Name = "Demo", City = "Nairobi", CountryCode = "KE", AddressLine1 = "Road", TimeZone = "Africa/Nairobi", IsActive = true },
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            Status = status,
            IsActive = true
        };
    }

    private static WhatsAppWebhookPayload BuildInboundPayload(string messageId, string from)
    {
        return new WhatsAppWebhookPayload
        {
            Object = "whatsapp_business_account",
            Entry = [new WhatsAppWebhookEntry
            {
                Changes = [new WhatsAppWebhookChange
                {
                    Field = "messages",
                    Value = new WhatsAppWebhookValue
                    {
                        Metadata = new WhatsAppWebhookMetadata { PhoneNumberId = "demo-phone-number-id" },
                        Messages = [new WhatsAppWebhookMessage
                        {
                            Id = messageId,
                            From = from,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                            Type = "text",
                            Text = new WhatsAppWebhookText { Body = "Hello from WhatsApp" }
                        }]
                    }
                }]
            }]
        };
    }

    private static WhatsAppWebhookPayload BuildStatusPayload(string messageId, string status)
    {
        return new WhatsAppWebhookPayload
        {
            Object = "whatsapp_business_account",
            Entry = [new WhatsAppWebhookEntry
            {
                Changes = [new WhatsAppWebhookChange
                {
                    Field = "messages",
                    Value = new WhatsAppWebhookValue
                    {
                        Metadata = new WhatsAppWebhookMetadata { PhoneNumberId = "demo-phone-number-id" },
                        Statuses = [new WhatsAppWebhookStatus
                        {
                            Id = messageId,
                            Status = status,
                            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                            RecipientId = "+14155551234"
                        }]
                    }
                }]
            }]
        };
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Repository = new FakeWhatsAppRepository();
            ChatService = new FakeChatService();
            ConversationService = new FakeConversationService(ConversationId);            Guest = new Guest
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                FirstName = "Ada",
                LastName = "Lovelace",
                PhoneNumber = "+14155551234",
                PreferredLanguage = "en",
                CountryCode = "US",
                IsActive = true
            };
            Repository.Guests.Add(Guest);
            Repository.Integration = new WhatsAppIntegration
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                DisplayName = "Demo",
                PhoneNumberId = "demo-phone-number-id",
                WhatsAppBusinessAccountId = "demo-waba-id",
                BusinessPhoneNumberMasked = "+1******1234",
                IsActive = true
            };
            Processor = new WhatsAppWebhookProcessor(
                Repository,
                new FakeConversationRepository(Repository.Messages),
                ChatService,
                ConversationService,
                new PhoneNumberNormalizer(),
                TenantAccessor,
                NullLogger<WhatsAppWebhookProcessor>.Instance);
            ChatService.TenantAccessor = TenantAccessor;
        }

        public TenantExecutionContextAccessor TenantAccessor { get; } = new();

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid PropertyId { get; } = Guid.NewGuid();
        public Guid ConversationId { get; } = Guid.NewGuid();
        public Guest Guest { get; }
        public FakeWhatsAppRepository Repository { get; }
        public FakeChatService ChatService { get; }
        public FakeConversationService ConversationService { get; }
        public WhatsAppWebhookProcessor Processor { get; }
    }

    private sealed class FakeWhatsAppRepository : IWhatsAppRepository
    {
        public WhatsAppIntegration? Integration { get; set; }
        public List<WhatsAppIntegration> Integrations { get; } = [];
        public List<WhatsAppTemplate> Templates { get; } = [];
        public List<Guest> Guests { get; } = [];
        public List<Reservation> Reservations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        private IEnumerable<WhatsAppIntegration> ScopedIntegrations
        {
            get
            {
                if (Integration is null)
                {
                    return Integrations;
                }

                return Integrations.Any(item => item.Id == Integration.Id)
                    ? Integrations
                    : [.. Integrations, Integration];
            }
        }

        public Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken)
            => Task.FromResult(ScopedIntegrations.FirstOrDefault(item => item.IsActive && item.PhoneNumberId == phoneNumberId));

        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListActiveIntegrationsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<WhatsAppIntegration>>(ScopedIntegrations.Where(item => item.IsActive).ToList());

        public Task<WhatsAppIntegration?> GetActiveIntegrationByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult(ScopedIntegrations.FirstOrDefault(item => item.IsActive && item.CompanyId == companyId));

        public Task<WhatsAppIntegration?> GetIntegrationForCompanyAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken)
            => Task.FromResult(ScopedIntegrations.FirstOrDefault(item => item.CompanyId == companyId && item.Id == integrationId));

        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListIntegrationsForCompanyAsync(Guid companyId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<WhatsAppIntegration> items = ScopedIntegrations
                .Where(item => item.CompanyId == companyId)
                .OrderByDescending(item => item.IsActive)
                .ThenBy(item => item.DisplayName)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<PagedResult<WhatsAppTemplate>> ListTemplatesAsync(Guid companyId, Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken)
        {
            var templates = Templates
                .Where(item => item.CompanyId == companyId && item.WhatsAppIntegrationId == integrationId);

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var status = query.Status.Trim();
                templates = templates.Where(item => item.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Language))
            {
                var language = query.Language.Trim();
                templates = templates.Where(item => item.LanguageCode == language);
            }

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                var category = query.Category.Trim();
                templates = templates.Where(item => item.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                templates = templates.Where(item =>
                    item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.BodyText.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            if (query.Active is { } active)
            {
                templates = templates.Where(item => item.IsActive == active);
            }

            if (query.ApprovedOnly == true)
            {
                templates = templates.Where(item => item.Status == "APPROVED");
            }

            var ordered = templates
                .OrderBy(item => item.Name)
                .ThenBy(item => item.LanguageCode)
                .ToList();

            var page = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;
            var pagedItems = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new PagedResult<WhatsAppTemplate>
            {
                Items = pagedItems,
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = ordered.Count
            });
        }

        public Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid integrationId, Guid templateId, CancellationToken cancellationToken)
            => Task.FromResult(Templates.FirstOrDefault(item =>
                item.CompanyId == companyId
                && item.WhatsAppIntegrationId == integrationId
                && item.Id == templateId));

        public Task<WhatsAppTemplate?> GetTemplateByNameAsync(Guid companyId, Guid integrationId, string name, string languageCode, CancellationToken cancellationToken)
            => Task.FromResult(Templates.FirstOrDefault(item =>
                item.CompanyId == companyId
                && item.WhatsAppIntegrationId == integrationId
                && item.Name == name
                && item.LanguageCode == languageCode));

        public Task<IReadOnlyCollection<WhatsAppTemplate>> ListTemplatesForIntegrationAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<WhatsAppTemplate> items = Templates
                .Where(item => item.CompanyId == companyId && item.WhatsAppIntegrationId == integrationId)
                .OrderBy(item => item.Name)
                .ThenBy(item => item.LanguageCode)
                .ToList();

            return Task.FromResult(items);
        }

        public Task<ConversationMessage?> GetLatestInboundGuestWhatsAppMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(Messages
                .Where(item => item.CompanyId == companyId
                    && item.ConversationId == conversationId
                    && !item.IsDeleted
                    && item.Provider == ConversationMessageProvider.WhatsAppCloud
                    && item.SenderType == ConversationSenderType.Guest)
                .OrderByDescending(item => item.SentAt)
                .FirstOrDefault());

        public Task<IReadOnlyCollection<Guest>> ListActiveGuestsWithPhoneAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Guest>>(Guests.Where(guest => guest.CompanyId == companyId && guest.IsActive && !guest.IsDeleted && guest.PhoneNumber != null).ToList());

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Reservation>>(Reservations.Where(item => item.CompanyId == companyId && item.PrimaryGuestId == guestId).ToList());

        public Task<ConversationMessage?> FindMessageByProviderExternalIdAsync(Guid companyId, ConversationMessageProvider provider, string externalMessageId, CancellationToken cancellationToken)
            => Task.FromResult(Messages.FirstOrDefault(item => item.CompanyId == companyId && item.Provider == provider && item.ExternalMessageId == externalMessageId));

        public Task AddTemplateAsync(WhatsAppTemplate template, CancellationToken cancellationToken)
        {
            Templates.Add(template);
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeConversationRepository(List<ConversationMessage> messages) : IConversationRepository
    {
        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid companyId, ConversationListQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ConversationMessage?> GetMessageForConversationAsync(Guid companyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken)
            => Task.FromResult(messages.FirstOrDefault(item =>
                item.CompanyId == companyId
                && item.ConversationId == conversationId
                && item.Id == messageId));
        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, Guid? reservationId, Guid? propertyId, DateTimeOffset cutoff, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ConversationParticipantReadState?> GetReadStateAsync(Guid companyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid companyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, ConversationMessageProvider? provider, CancellationToken cancellationToken)
            => Task.FromResult(messages.FirstOrDefault(item => item.CompanyId == companyId && item.ExternalMessageId == externalMessageId && (provider is null || item.Provider == provider)));
        public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<User?> GetUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddReadStateAsync(ConversationParticipantReadState state, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeChatService : IChatService
    {
        public SendChatMessageRequest? Request { get; private set; }
        public int SendCallCount { get; private set; }
        public List<Guid?> ObservedTenantCompanyIds { get; } = [];
        public ITenantExecutionContextAccessor? TenantAccessor { get; set; }

        public Task<ApiResponse<ChatMessageResponse>> SendGuestMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            SendCallCount++;
            ObservedTenantCompanyIds.Add(TenantAccessor?.CompanyId);
            return Task.FromResult(ApiResponse<ChatMessageResponse>.Ok(new ChatMessageResponse
            {
                ConversationId = Guid.NewGuid(),
                ConversationStatus = ConversationStatus.Open,
                GuestMessage = new DTOs.Chat.ChatVisibleMessageDto
                {
                    Id = Guid.NewGuid(),
                    ConversationId = Guid.NewGuid(),
                    SenderType = ConversationSenderType.Guest,
                    Content = request.Message,
                    MessageType = ConversationMessageType.Text,
                    SentAt = request.CurrentTimestamp ?? DateTimeOffset.UtcNow
                },
                CreatedAt = DateTimeOffset.UtcNow
            }));
        }

        public Task<ApiResponse<ChatConversationResponse>> GetGuestConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ChatHistoryResponse>> GetGuestHistoryAsync(Guid conversationId, ChatHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ChatStatusResponse>> EscalateGuestConversationAsync(Guid conversationId, EscalateChatRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ChatStatusResponse>> EndGuestConversationAsync(Guid conversationId, EndChatRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ChatMessageResponse>> ConfirmPendingActionAsync(Guid conversationId, Guid actionId, ConfirmPendingActionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ChatMessageResponse>> CancelPendingActionAsync(Guid conversationId, Guid actionId, CancelPendingActionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeConversationService(Guid conversationId) : IConversationService
    {
        public CreateConversationRequest? CreatedConversationRequest { get; private set; }
        public AddGuestMessageRequest? LastGuestMessageRequest { get; private set; }
        public bool HumanTakeoverEnabled { get; private set; }
        public ConversationMessageDeliveryStatus? LastDeliveryStatus { get; private set; }
        public Guid? RetriedConversationId { get; private set; }
        public Guid? RetriedMessageId { get; private set; }
        public ApiResponse<ConversationMessageResponse> RetryResult { get; set; } = ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.Empty,
            SenderType = ConversationSenderType.Host,
            MessageType = ConversationMessageType.Text,
            Content = "retry",
            SentAt = DateTimeOffset.UtcNow
        });

        public Task<ApiResponse<ConversationListResponse>> GetConversationsAsync(ConversationListQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken)
        {
            CreatedConversationRequest = request;
            return Task.FromResult(ApiResponse<ConversationDetailResponse>.Ok(new ConversationDetailResponse
            {
                Id = conversationId,
                ConversationId = conversationId,
                GuestId = request.GuestId,
                Channel = request.Channel,
                StartedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow,
                Status = ConversationStatus.Open,
                HumanTakeoverEnabled = false,
                RequiresHostAttention = false,
                Guest = new ConversationGuestSummary
                {
                    Id = request.GuestId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    FullName = "Ada Lovelace",
                    PreferredLanguage = "en"
                }
            }));
        }

        public Task<ApiResponse<ConversationDetailResponse>> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationHistoryResponse>> GetConversationHistoryAsync(Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<ApiResponse<ConversationMessageResponse>> AddGuestMessageAsync(Guid currentConversationId, AddGuestMessageRequest request, CancellationToken cancellationToken)
        {
            LastGuestMessageRequest = request;
            return Task.FromResult(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
            {
                Id = Guid.NewGuid(),
                ConversationId = currentConversationId,
                SenderType = ConversationSenderType.Guest,
                MessageType = ConversationMessageType.Text,
                Content = request.Content,
                Provider = request.Provider,
                SentAt = request.SentAt ?? DateTimeOffset.UtcNow
            }));
        }

        public Task<ApiResponse<ConversationMessageResponse>> AddAIMessageAsync(Guid conversationId, string content, DTOs.AIOrchestration.AIOrchestrationResult result, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddHostMessageAsync(Guid conversationId, AddHostMessageRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> RetryFailedMessageAsync(Guid currentConversationId, Guid messageId, CancellationToken cancellationToken)
        {
            RetriedConversationId = currentConversationId;
            RetriedMessageId = messageId;
            return Task.FromResult(RetryResult);
        }
        public Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken) => Task.FromResult(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse { Id = Guid.NewGuid(), ConversationId = conversationId, SenderType = ConversationSenderType.System, MessageType = ConversationMessageType.InternalNote, Content = request.Content, IsInternal = true, SentAt = DateTimeOffset.UtcNow }));
        public Task<ApiResponse<ConversationMessageResponse>> AddPaymentConfirmationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<ApiResponse<ConversationMessageResponse>> UpdateMessageDeliveryStatusAsync(Guid currentConversationId, Guid messageId, ConversationMessageDeliveryStatus status, DateTimeOffset occurredAt, string? failureCode, string? failureReason, CancellationToken cancellationToken)
        {
            LastDeliveryStatus = status;
            return Task.FromResult(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse
            {
                Id = messageId,
                ConversationId = currentConversationId,
                SenderType = ConversationSenderType.Host,
                MessageType = ConversationMessageType.Text,
                DeliveryStatus = status,
                SentAt = occurredAt
            }));
        }

        public Task<ApiResponse<ConversationDetailResponse>> EscalateConversationAsync(Guid conversationId, EscalateConversationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationDetailResponse>> EnableHumanTakeoverAsync(Guid currentConversationId, CancellationToken cancellationToken)
        {
            HumanTakeoverEnabled = true;
            return Task.FromResult(ApiResponse<ConversationDetailResponse>.Ok(new ConversationDetailResponse
            {
                Id = currentConversationId,
                ConversationId = currentConversationId,
                GuestId = Guid.NewGuid(),
                Channel = GuestChannel.WhatsApp,
                StartedAt = DateTimeOffset.UtcNow,
                LastActivityAt = DateTimeOffset.UtcNow,
                Status = ConversationStatus.HumanManaged,
                HumanTakeoverEnabled = true,
                RequiresHostAttention = true,
                Guest = new ConversationGuestSummary
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    FullName = "Ada Lovelace",
                    PreferredLanguage = "en"
                }
            }));
        }

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