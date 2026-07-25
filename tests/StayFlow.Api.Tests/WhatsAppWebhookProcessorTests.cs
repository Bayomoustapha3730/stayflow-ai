using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Common;
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
            ConversationService = new FakeConversationService(ConversationId);
            Guest = new Guest
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
                new TenantExecutionContextAccessor(),
                NullLogger<WhatsAppWebhookProcessor>.Instance);
        }

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
        public List<Guest> Guests { get; } = [];
        public List<Reservation> Reservations { get; } = [];
        public List<ConversationMessage> Messages { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken)
            => Task.FromResult(Integration?.PhoneNumberId == phoneNumberId ? Integration : null);

        public Task<WhatsAppIntegration?> GetActiveIntegrationByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult(Integration?.CompanyId == companyId ? Integration : null);

        public Task<IReadOnlyCollection<Guest>> ListActiveGuestsWithPhoneAsync(Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Guest>>(Guests.Where(guest => guest.CompanyId == companyId && guest.IsActive).ToList());

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<Reservation>>(Reservations.Where(item => item.CompanyId == companyId && item.PrimaryGuestId == guestId).ToList());

        public Task<ConversationMessage?> FindMessageByProviderExternalIdAsync(Guid companyId, ConversationMessageProvider provider, string externalMessageId, CancellationToken cancellationToken)
            => Task.FromResult(Messages.FirstOrDefault(item => item.CompanyId == companyId && item.Provider == provider && item.ExternalMessageId == externalMessageId));

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
        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, DateTimeOffset cutoff, CancellationToken cancellationToken) => throw new NotImplementedException();
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

        public Task<ApiResponse<ChatMessageResponse>> SendGuestMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken)
        {
            Request = request;
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
    }

    private sealed class FakeConversationService(Guid conversationId) : IConversationService
    {
        public CreateConversationRequest? CreatedConversationRequest { get; private set; }
        public AddGuestMessageRequest? LastGuestMessageRequest { get; private set; }
        public bool HumanTakeoverEnabled { get; private set; }
        public ConversationMessageDeliveryStatus? LastDeliveryStatus { get; private set; }

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
        public Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken) => Task.FromResult(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse { Id = Guid.NewGuid(), ConversationId = conversationId, SenderType = ConversationSenderType.System, MessageType = ConversationMessageType.InternalNote, Content = request.Content, IsInternal = true, SentAt = DateTimeOffset.UtcNow }));

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
    }
}