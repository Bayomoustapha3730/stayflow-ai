using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class GuestJourneyMessageDeliveryProcessorTests
{
    [Fact]
    public async Task ProcessDueAsync_SuccessfulDelivery_MarksAcceptedWithProviderMessageId()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationMessage = NewConversationMessage(ConversationMessageDeliveryStatus.Sent, externalMessageId: "wamid.123");
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Ok(NewResponse(conversationMessage.Id)));
        var conversationRepository = new FakeConversationRepository(conversationMessage);

        var processor = CreateProcessor(repository, conversationService, conversationRepository);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(GuestJourneyMessageStatus.Accepted, message.Status);
        Assert.Equal("wamid.123", message.ProviderMessageId);
        Assert.Equal(conversationMessage.Id, message.ConversationMessageId);
    }

    [Fact]
    public async Task ProcessDueAsync_TransientFailure_MarksRetryableFailed()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationMessage = NewConversationMessage(ConversationMessageDeliveryStatus.Failed, failureCode: "ProviderUnavailable");
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Ok(NewResponse(conversationMessage.Id)));
        var conversationRepository = new FakeConversationRepository(conversationMessage);

        var processor = CreateProcessor(repository, conversationService, conversationRepository);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(GuestJourneyMessageStatus.Failed, message.Status);
        Assert.NotNull(message.NextAttemptAtUtc);
    }

    [Fact]
    public async Task ProcessDueAsync_ServiceWindowClosed_MarksBlockedNotFailed()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationMessage = NewConversationMessage(ConversationMessageDeliveryStatus.Failed, failureCode: "CustomerServiceWindowClosed");
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Ok(NewResponse(conversationMessage.Id)));
        var conversationRepository = new FakeConversationRepository(conversationMessage);

        var processor = CreateProcessor(repository, conversationService, conversationRepository);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Blocked);
        Assert.Equal(GuestJourneyMessageStatus.Blocked, message.Status);
    }

    [Fact]
    public async Task ProcessDueAsync_MissingWhatsAppIntegration_MarksBlockedNotAccepted()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: false);
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("unused"));
        var conversationRepository = new FakeConversationRepository(null);

        var processor = CreateProcessor(repository, conversationService, conversationRepository);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Blocked);
        Assert.Equal(GuestJourneyMessageStatus.Blocked, message.Status);
        Assert.Equal(0, conversationService.CallCount);
    }

    [Theory]
    [InlineData(nameof(ReservationStatus.Cancelled))]
    [InlineData(nameof(ReservationStatus.NoShow))]
    public async Task ProcessDueAsync_ReservationNoLongerEligible_MarksSuppressedWithoutSending(string statusName)
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent, status: Enum.Parse<ReservationStatus>(statusName));
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("unused"));

        var processor = CreateProcessor(repository, conversationService, new FakeConversationRepository(null));
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Suppressed);
        Assert.Equal(GuestJourneyMessageStatus.Suppressed, message.Status);
        Assert.Equal(0, conversationService.CallCount);
    }

    [Fact]
    public async Task ProcessDueAsync_ObsoleteAnchorAfterDateChange_MarksSuppressed()
    {
        var lifecycleEvent = NewLifecycleEvent(propertyLocalDate: new DateOnly(2026, 8, 10));
        var reservation = NewReservation(lifecycleEvent, checkInDate: new DateOnly(2026, 9, 20), checkOutDate: new DateOnly(2026, 9, 24));
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("unused"));

        var processor = CreateProcessor(repository, conversationService, new FakeConversationRepository(null));
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Suppressed);
        Assert.Equal(GuestJourneyMessageStatus.Suppressed, message.Status);
    }

    [Fact]
    public async Task ProcessDueAsync_TenantIdentityMismatch_MarksSuppressed()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        reservation.PropertyId = Guid.NewGuid();
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("unused"));

        var processor = CreateProcessor(repository, conversationService, new FakeConversationRepository(null));
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Suppressed);
        Assert.Equal(GuestJourneyMessageStatus.Suppressed, message.Status);
    }

    [Fact]
    public async Task ProcessDueAsync_OneMessageFailureDoesNotBlockAnother()
    {
        var lifecycleEventA = NewLifecycleEvent();
        var reservationA = NewReservation(lifecycleEventA);
        var messageA = NewMessage(lifecycleEventA, conversationId: Guid.NewGuid());

        var lifecycleEventB = NewLifecycleEvent();
        var reservationB = NewReservation(lifecycleEventB);
        var messageB = NewMessage(lifecycleEventB, conversationId: Guid.NewGuid());

        var repository = new FakeRepository([messageA, messageB], lifecycleEventA, reservationA, hasIntegration: true);
        repository.AdditionalLifecycleEvents[lifecycleEventB.Id] = (lifecycleEventB, reservationB);

        var conversationMessage = NewConversationMessage(ConversationMessageDeliveryStatus.Sent, externalMessageId: "wamid.ok");
        var conversationService = new ThrowingThenSucceedingConversationService(messageA.ConversationId!.Value, NewResponse(conversationMessage.Id));
        var conversationRepository = new FakeConversationRepository(conversationMessage);

        var processor = CreateProcessor(repository, conversationService, conversationRepository);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Accepted);
        Assert.Equal(GuestJourneyMessageStatus.Failed, messageA.Status);
        Assert.Equal(GuestJourneyMessageStatus.Accepted, messageB.Status);
    }

    [Fact]
    public async Task ProcessDueAsync_CancellationIsNotPersistedAsFailure()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        using var cancellationTokenSource = new CancellationTokenSource();
        var conversationService = new CancellingConversationService(cancellationTokenSource);

        var processor = CreateProcessor(repository, conversationService, new FakeConversationRepository(null));

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessDueAsync(cancellationTokenSource.Token));

        Assert.Equal(GuestJourneyMessageStatus.Processing, message.Status);
    }

    [Fact]
    public async Task OpenWindow_UsesFreeFormLifecycleMessage()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationMessage = NewConversationMessage(ConversationMessageDeliveryStatus.Sent, externalMessageId: "wamid.open");
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Ok(NewResponse(conversationMessage.Id)));
        var templateResolver = new RecordingTemplateResolver(ReservationLifecycleTemplateResolution.Blocked("not used"));
        var templateService = new RecordingWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Fail("not used"));

        var processor = CreateProcessor(
            repository,
            conversationService,
            new FakeConversationRepository(conversationMessage),
            new ConfigurableWindowEvaluator(isOpen: true),
            templateResolver,
            templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, conversationService.CallCount);
        Assert.Equal(0, templateResolver.CallCount);
        Assert.Equal(0, templateService.CallCount);
        Assert.Equal(GuestJourneyMessageStatus.Accepted, message.Status);
        Assert.Null(message.DeliveredAtUtc);
        Assert.Equal("wamid.open", message.ProviderMessageId);
    }

    [Fact]
    public async Task ClosedWindow_WithApprovedMappedTemplate_UsesTemplate()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationMessage = NewConversationMessage(ConversationMessageDeliveryStatus.Sent, externalMessageId: "wamid.template");
        conversationMessage.IsTemplateMessage = true;
        conversationMessage.TemplateName = "tenant_approved_template";
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("free form must not be used"));
        var template = new WhatsAppTemplate { Id = Guid.NewGuid(), CompanyId = lifecycleEvent.CompanyId, WhatsAppIntegrationId = repository.IntegrationId, Name = "tenant_approved_template", LanguageCode = "en", Status = "APPROVED", IsActive = true };
        var templateResolver = new RecordingTemplateResolver(new ReservationLifecycleTemplateResolution(true, template, ["Ada"], null));
        var templateService = new RecordingWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Ok(NewResponse(conversationMessage.Id)));

        var processor = CreateProcessor(
            repository,
            conversationService,
            new FakeConversationRepository(conversationMessage),
            new ConfigurableWindowEvaluator(isOpen: false),
            templateResolver,
            templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, conversationService.CallCount);
        Assert.Equal(1, templateResolver.CallCount);
        Assert.Equal(1, templateService.CallCount);
        Assert.True(conversationMessage.IsTemplateMessage);
        Assert.Equal(ConversationMessageType.LifecycleAutomation, conversationMessage.MessageType);
        Assert.Equal("wamid.template", message.ProviderMessageId);
        Assert.Equal(GuestJourneyMessageStatus.Accepted, message.Status);
        Assert.Null(message.DeliveredAtUtc);
    }

    [Fact]
    public async Task ClosedWindow_WithoutTemplate_BlocksWithoutFreeFormFallback()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("free form must not be used"));
        var templateResolver = new RecordingTemplateResolver(ReservationLifecycleTemplateResolution.Blocked("No enabled WhatsApp template mapping is configured for ArrivalDay."));
        var templateService = new RecordingWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Fail("not used"));

        var processor = CreateProcessor(
            repository,
            conversationService,
            new FakeConversationRepository(null),
            new ConfigurableWindowEvaluator(isOpen: false),
            templateResolver,
            templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Blocked);
        Assert.Equal(0, conversationService.CallCount);
        Assert.Equal(1, templateResolver.CallCount);
        Assert.Equal(0, templateService.CallCount);
        Assert.Equal(GuestJourneyMessageStatus.Blocked, message.Status);
    }

    [Fact]
    public async Task ClosedWindow_WithDisabledMapping_Blocks()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("free form must not be used"));
        var templateResolver = new RecordingTemplateResolver(ReservationLifecycleTemplateResolution.Blocked("No enabled WhatsApp template mapping is configured for ArrivalDay."));
        var templateService = new RecordingWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Fail("not used"));

        var processor = CreateProcessor(repository, conversationService, new FakeConversationRepository(null), new ConfigurableWindowEvaluator(false), templateResolver, templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Blocked);
        Assert.Equal(GuestJourneyMessageStatus.Blocked, message.Status);
        Assert.Equal(0, conversationService.CallCount);
        Assert.Equal(0, templateService.CallCount);
    }

    [Fact]
    public async Task ClosedWindow_WithUnapprovedTemplate_Blocks()
    {
        var lifecycleEvent = NewLifecycleEvent();
        var reservation = NewReservation(lifecycleEvent);
        var message = NewMessage(lifecycleEvent, conversationId: Guid.NewGuid());
        var repository = new FakeRepository([message], lifecycleEvent, reservation, hasIntegration: true);
        var conversationService = new FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Fail("free form must not be used"));
        var templateResolver = new RecordingTemplateResolver(ReservationLifecycleTemplateResolution.Blocked("Configured lifecycle template is not approved for sending."));
        var templateService = new RecordingWhatsAppTemplateService(ApiResponse<ConversationMessageResponse>.Fail("not used"));

        var processor = CreateProcessor(repository, conversationService, new FakeConversationRepository(null), new ConfigurableWindowEvaluator(false), templateResolver, templateService);
        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Blocked);
        Assert.Equal(GuestJourneyMessageStatus.Blocked, message.Status);
        Assert.Equal(0, conversationService.CallCount);
        Assert.Equal(0, templateService.CallCount);
    }

    private static GuestJourneyMessageDeliveryProcessor CreateProcessor(
        FakeRepository repository,
        IConversationService conversationService,
        Repositories.IConversationRepository conversationRepository)
    {
        return CreateProcessor(
            repository,
            conversationService,
            conversationRepository,
            new OpenWindowEvaluator(),
            new BlockingTemplateResolver(),
            new BlockingWhatsAppTemplateService());
    }

    private static GuestJourneyMessageDeliveryProcessor CreateProcessor(
        FakeRepository repository,
        IConversationService conversationService,
        Repositories.IConversationRepository conversationRepository,
        IWhatsAppCustomerServiceWindowEvaluator windowEvaluator,
        IReservationLifecycleWhatsAppTemplateResolver templateResolver,
        IWhatsAppTemplateService whatsAppTemplateService)
    {
        return new GuestJourneyMessageDeliveryProcessor(
            repository,
            conversationService,
            conversationRepository,
            windowEvaluator,
            templateResolver,
            whatsAppTemplateService,
            new ReservationLifecycleEventIdempotencyKeyBuilder(),
            new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero)),
            Options.Create(new GuestJourneyDeliveryOptions { BatchSize = 10 }),
            Options.Create(new ReservationContextOptions { PreArrivalWindowDays = 7 }),
            NullLogger<GuestJourneyMessageDeliveryProcessor>.Instance);
    }

    private static ReservationLifecycleEvent NewLifecycleEvent(DateOnly? propertyLocalDate = null)
    {
        var companyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var localDate = propertyLocalDate ?? new DateOnly(2026, 8, 10);
        return new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ReservationId = reservationId,
            PropertyId = Guid.NewGuid(),
            GuestId = Guid.NewGuid(),
            EventType = ReservationLifecycleEventType.ArrivalDay,
            RuleVersion = ReservationLifecycleRuleVersions.V1,
            PropertyLocalDate = localDate,
            ScheduledForUtc = new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero),
            Status = ReservationLifecycleEventStatus.Pending,
            IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(companyId, reservationId, ReservationLifecycleEventType.ArrivalDay, localDate, ReservationLifecycleRuleVersions.V1)
        };
    }

    private static Reservation NewReservation(ReservationLifecycleEvent lifecycleEvent, ReservationStatus status = ReservationStatus.Confirmed, DateOnly? checkInDate = null, DateOnly? checkOutDate = null)
    {
        var checkIn = checkInDate ?? lifecycleEvent.PropertyLocalDate;
        return new Reservation
        {
            Id = lifecycleEvent.ReservationId,
            CompanyId = lifecycleEvent.CompanyId,
            PropertyId = lifecycleEvent.PropertyId,
            PrimaryGuestId = lifecycleEvent.GuestId,
            ReservationSource = "Manual",
            CheckInDate = checkIn,
            CheckOutDate = checkOutDate ?? checkIn.AddDays(4),
            Adults = 1,
            Status = status,
            IsActive = true
        };
    }

    private static GuestJourneyMessage NewMessage(ReservationLifecycleEvent lifecycleEvent, Guid? conversationId)
    {
        return new GuestJourneyMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = lifecycleEvent.CompanyId,
            ReservationId = lifecycleEvent.ReservationId,
            ReservationLifecycleEventId = lifecycleEvent.Id,
            PropertyId = lifecycleEvent.PropertyId,
            GuestId = lifecycleEvent.GuestId,
            ConversationId = conversationId,
            JourneyEventType = lifecycleEvent.EventType,
            Language = "en",
            RenderedContent = "Hi Ada, today is your check-in day at Demo Property.",
            Status = GuestJourneyMessageStatus.Processing,
            AttemptCount = 1,
            LastAttemptAtUtc = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero),
            IdempotencyKey = lifecycleEvent.IdempotencyKey
        };
    }

    private static ConversationMessage NewConversationMessage(ConversationMessageDeliveryStatus deliveryStatus, string? externalMessageId = null, string? failureCode = null)
    {
        return new ConversationMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            SenderType = ConversationSenderType.System,
            MessageType = ConversationMessageType.LifecycleAutomation,
            Content = "Hi Ada, today is your check-in day at Demo Property.",
            DeliveryStatus = deliveryStatus,
            ExternalMessageId = externalMessageId,
            FailureCode = failureCode,
            SentAt = DateTimeOffset.UtcNow
        };
    }

    private static ConversationMessageResponse NewResponse(Guid id)
    {
        return new ConversationMessageResponse
        {
            Id = id,
            ConversationId = Guid.NewGuid(),
            SenderType = ConversationSenderType.System,
            MessageType = ConversationMessageType.LifecycleAutomation,
            Content = "Hi Ada, today is your check-in day at Demo Property.",
            SentAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeRepository(
        List<GuestJourneyMessage> claimable,
        ReservationLifecycleEvent primaryLifecycleEvent,
        Reservation primaryReservation,
        bool hasIntegration) : IGuestJourneyMessageRepository
    {
        public Guid IntegrationId { get; } = Guid.NewGuid();
        public Dictionary<Guid, (ReservationLifecycleEvent Event, Reservation Reservation)> AdditionalLifecycleEvents { get; } = [];

        public Task<IReadOnlyCollection<GuestJourneyMessage>> ClaimDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
        {
            foreach (var message in claimable)
            {
                message.Status = GuestJourneyMessageStatus.Processing;
            }

            return Task.FromResult<IReadOnlyCollection<GuestJourneyMessage>>(claimable.Take(batchSize).ToList());
        }

        public Task<int> RecoverStaleProcessingAsync(DateTimeOffset staleBeforeUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<int> RecoverRetryableFailedAsync(DateTimeOffset nowUtc, int maxAttempts, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<ReservationLifecycleEvent?> GetLifecycleEventForDeliveryAsync(GuestJourneyMessage message, CancellationToken cancellationToken)
        {
            if (message.ReservationLifecycleEventId == primaryLifecycleEvent.Id)
            {
                primaryLifecycleEvent.Reservation = primaryReservation;
                primaryLifecycleEvent.Property = new Property { Id = primaryLifecycleEvent.PropertyId, CompanyId = primaryLifecycleEvent.CompanyId, Name = "Demo Property", AddressLine1 = "Road", City = "Nairobi", CountryCode = "KE", TimeZone = "Africa/Nairobi", IsActive = true };
                primaryLifecycleEvent.Guest = new Guest { Id = primaryLifecycleEvent.GuestId, CompanyId = primaryLifecycleEvent.CompanyId, FirstName = "Ada", LastName = "Guest", PreferredLanguage = "en", CountryCode = "KE", IsActive = true };
                return Task.FromResult<ReservationLifecycleEvent?>(primaryLifecycleEvent);
            }

            if (AdditionalLifecycleEvents.TryGetValue(message.ReservationLifecycleEventId, out var entry))
            {
                entry.Event.Reservation = entry.Reservation;
                entry.Event.Property = new Property { Id = entry.Event.PropertyId, CompanyId = entry.Event.CompanyId, Name = "Demo Property", AddressLine1 = "Road", City = "Nairobi", CountryCode = "KE", TimeZone = "Africa/Nairobi", IsActive = true };
                entry.Event.Guest = new Guest { Id = entry.Event.GuestId, CompanyId = entry.Event.CompanyId, FirstName = "Ada", LastName = "Guest", PreferredLanguage = "en", CountryCode = "KE", IsActive = true };
                return Task.FromResult<ReservationLifecycleEvent?>(entry.Event);
            }

            return Task.FromResult<ReservationLifecycleEvent?>(null);
        }

        public Task<WhatsAppIntegration?> GetActiveWhatsAppIntegrationAsync(Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(hasIntegration ? new WhatsAppIntegration { Id = IntegrationId, CompanyId = companyId, IsActive = true } : null);
        }

        public Task MarkAcceptedAsync(GuestJourneyMessage message, Guid conversationMessageId, string? providerMessageId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            message.Status = GuestJourneyMessageStatus.Accepted;
            message.ConversationMessageId = conversationMessageId;
            message.ProviderMessageId = providerMessageId;
            message.AcceptedAtUtc = nowUtc;
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(GuestJourneyMessage message, string error, DateTimeOffset nextAttemptAtUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            message.Status = GuestJourneyMessageStatus.Failed;
            message.LastError = error;
            message.NextAttemptAtUtc = nextAttemptAtUtc;
            return Task.CompletedTask;
        }

        public Task MarkBlockedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            message.Status = GuestJourneyMessageStatus.Blocked;
            message.LastError = reason;
            return Task.CompletedTask;
        }

        public Task MarkSuppressedAsync(GuestJourneyMessage message, string reason, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            message.Status = GuestJourneyMessageStatus.Suppressed;
            message.LastError = reason;
            return Task.CompletedTask;
        }

        public Task<ReservationLifecycleEvent?> GetLifecycleEventContextAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<GuestJourneyMessage?> GetByLifecycleEventAsync(Guid companyId, Guid lifecycleEventId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Conversation?> GetLatestConversationForReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<GuestJourneyMessage?> FindByConversationMessageAsync(Guid companyId, Guid conversationMessageId, CancellationToken cancellationToken) => Task.FromResult<GuestJourneyMessage?>(null);
        public Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task AddAsync(GuestJourneyMessage message, CancellationToken cancellationToken) => throw new NotImplementedException();
        public void Detach(GuestJourneyMessage message) => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class OpenWindowEvaluator : IWhatsAppCustomerServiceWindowEvaluator
    {
        public Task<WhatsAppCustomerServiceWindowEvaluation> EvaluateAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WhatsAppCustomerServiceWindowEvaluation { IsOpen = true, Reason = "open" });
        }
    }

    private sealed class ConfigurableWindowEvaluator(bool isOpen) : IWhatsAppCustomerServiceWindowEvaluator
    {
        public Task<WhatsAppCustomerServiceWindowEvaluation> EvaluateAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WhatsAppCustomerServiceWindowEvaluation { IsOpen = isOpen, Reason = isOpen ? "open" : "closed" });
        }
    }

    private sealed class BlockingTemplateResolver : IReservationLifecycleWhatsAppTemplateResolver
    {
        public Task<ReservationLifecycleTemplateResolution> ResolveAsync(Guid companyId, Guid integrationId, ReservationLifecycleEventType eventType, string? guestPreferredLanguage, Reservation reservation, Property property, Guest guest, CancellationToken cancellationToken)
        {
            return Task.FromResult(ReservationLifecycleTemplateResolution.Blocked("not used"));
        }
    }

    private sealed class RecordingTemplateResolver(ReservationLifecycleTemplateResolution result) : IReservationLifecycleWhatsAppTemplateResolver
    {
        public int CallCount { get; private set; }

        public Task<ReservationLifecycleTemplateResolution> ResolveAsync(Guid companyId, Guid integrationId, ReservationLifecycleEventType eventType, string? guestPreferredLanguage, Reservation reservation, Property property, Guest guest, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class BlockingWhatsAppTemplateService : IWhatsAppTemplateService
    {
        public Task<ApiResponse<ConversationMessageResponse>> SendLifecycleAutomationTemplateMessageAsync(Guid companyId, Guid conversationId, Guid integrationId, Guid templateId, IReadOnlyCollection<string> variables, string idempotencyKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(ApiResponse<ConversationMessageResponse>.Fail("not used"));
        }

        public Task<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>> GetIntegrationsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppIntegrationHealthResponse>> CheckHealthAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateSyncResponse>> SyncTemplatesAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateListResponse>> ListTemplatesAsync(Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateDetailResponse>> GetTemplateAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplatePreviewResponse>> PreviewTemplateAsync(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> SendTemplateMessageAsync(Guid conversationId, Guid templateId, SendWhatsAppTemplateMessageRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>> GetCustomerServiceWindowStatusAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class RecordingWhatsAppTemplateService(ApiResponse<ConversationMessageResponse> result) : IWhatsAppTemplateService
    {
        public int CallCount { get; private set; }

        public Task<ApiResponse<ConversationMessageResponse>> SendLifecycleAutomationTemplateMessageAsync(Guid companyId, Guid conversationId, Guid integrationId, Guid templateId, IReadOnlyCollection<string> variables, string idempotencyKey, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }

        public Task<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>> GetIntegrationsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppIntegrationHealthResponse>> CheckHealthAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateSyncResponse>> SyncTemplatesAsync(Guid integrationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateListResponse>> ListTemplatesAsync(Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplateDetailResponse>> GetTemplateAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppTemplatePreviewResponse>> PreviewTemplateAsync(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> SendTemplateMessageAsync(Guid conversationId, Guid templateId, SendWhatsAppTemplateMessageRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>> GetCustomerServiceWindowStatusAsync(Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeConversationRepository(ConversationMessage? message) : Repositories.IConversationRepository
    {
        public Task<ConversationMessage?> GetMessageForConversationAsync(Guid companyId, Guid conversationId, Guid messageId, CancellationToken cancellationToken) => Task.FromResult(message);

        public Task<PagedResult<ConversationSummaryResponse>> ListConversationsAsync(Guid companyId, ConversationListQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> GetTotalUnreadCountForHostAsync(Guid companyId, Guid hostUserId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Dictionary<Guid, int>> GetUnreadMessageCountsForHostAsync(Guid companyId, Guid hostUserId, IReadOnlyCollection<Guid> conversationIds, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<int> GetUnreadHostMessageCountForGuestAsync(Guid companyId, Guid guestId, Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Conversation?> GetByIdForCompanyAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Conversation?> GetOpenConversationAsync(Guid companyId, Guid guestId, GuestChannel channel, string? channelIdentity, Guid? reservationId, Guid? propertyId, DateTimeOffset cutoff, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<PagedResult<ConversationMessage>> GetMessagesAsync(Guid companyId, Guid conversationId, ConversationHistoryQueryParameters query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ConversationMessage?> GetLatestVisibleMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ConversationParticipantReadState?> GetReadStateAsync(Guid companyId, Guid conversationId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ConversationParticipantReadState>> GetReadStatesForParticipantAsync(Guid companyId, ConversationParticipantKind participantKind, Guid participantId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ConversationMessage?> FindByExternalMessageIdAsync(Guid companyId, string externalMessageId, ConversationMessageProvider? provider, CancellationToken cancellationToken) => throw new NotImplementedException();
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

    private class FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse> response) : IConversationService
    {
        public int CallCount { get; private set; }

        public virtual Task<ApiResponse<ConversationMessageResponse>> AddLifecycleAutomationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response);
        }

        public Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
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

    private sealed class ThrowingThenSucceedingConversationService(Guid failingConversationId, ConversationMessageResponse successResponse) : FakeConversationServiceForDelivery(ApiResponse<ConversationMessageResponse>.Ok(successResponse))
    {
        public override Task<ApiResponse<ConversationMessageResponse>> AddLifecycleAutomationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken)
        {
            if (conversationId == failingConversationId)
            {
                throw new InvalidOperationException("Simulated transient send failure.");
            }

            return base.AddLifecycleAutomationMessageAsync(companyId, conversationId, content, idempotencyKey, cancellationToken);
        }
    }

    private sealed class CancellingConversationService(CancellationTokenSource cancellationTokenSource) : IConversationService
    {
        public Task<ApiResponse<ConversationMessageResponse>> AddLifecycleAutomationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken)
        {
            cancellationTokenSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable.");
        }

        public Task<ApiResponse<ConversationDetailResponse>> CreateOrGetConversationAsync(CreateConversationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
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
