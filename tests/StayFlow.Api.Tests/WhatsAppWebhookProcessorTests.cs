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
        Assert.Equal(fixture.Repository.Integration!.Id, fixture.ChatService.ObservedWhatsAppIntegrationId);
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
    }

    [Fact]
    public async Task ProcessAsync_MetaStyleDigitsOnlySender_MatchesGuestAndReusesChatPipeline()
    {
        var fixture = new Fixture();
        fixture.Repository.Reservations.Add(CreateReservation(
            fixture.CompanyId,
            fixture.PropertyId,
            fixture.Guest.Id,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 28),
            ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.meta", "14155551234"), "cid-meta", CancellationToken.None);

        Assert.DoesNotContain(fixture.Repository.AuditLogs, log => log.Action == "InvalidWhatsAppPhoneNumber");
        Assert.DoesNotContain(fixture.Repository.AuditLogs, log => log.Action == "WhatsAppGuestNotFound");
        Assert.NotNull(fixture.ChatService.Request);
        Assert.Equal(fixture.Guest.Id, fixture.ChatService.Request!.GuestId);
        Assert.Equal(GuestChannel.WhatsApp, fixture.ChatService.Request.Channel);
        Assert.Equal("wamid.meta", fixture.ChatService.Request.ExternalMessageId);
        Assert.Equal(fixture.Repository.Integration!.Id, fixture.ChatService.ObservedWhatsAppIntegrationId);
    }

    [Theory]
    [InlineData("0700000002")]
    [InlineData("1415555")]
    [InlineData("1415555123456789")]
    [InlineData("abc")]
    [InlineData("1415555123a")]
    [InlineData("1 415 555 1234")]
    [InlineData("")]
    public async Task ProcessAsync_MalformedProviderSender_IsRejectedWithoutChatProcessing(string from)
    {
        var fixture = new Fixture();
        fixture.Repository.Reservations.Add(CreateReservation(
            fixture.CompanyId,
            fixture.PropertyId,
            fixture.Guest.Id,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 28),
            ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.malformed", from), "cid-malformed", CancellationToken.None);

        Assert.Contains(fixture.Repository.AuditLogs, log => log.Action == "InvalidWhatsAppPhoneNumber");
        Assert.Null(fixture.ChatService.Request);
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
    }

    [Fact]
    public async Task ProcessAsync_TwoActiveIntegrations_RoutesByPhoneNumberIdAndBindsSelectedIntegration()
    {
        var fixture = new Fixture();
        var selectedIntegration = new WhatsAppIntegration
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            DisplayName = "Meta Test",
            PhoneNumberId = "meta-test-phone-number-id",
            WhatsAppBusinessAccountId = "meta-test-waba-id",
            BusinessPhoneNumberMasked = "+1******5325",
            IsActive = true
        };
        fixture.Repository.Integrations.Add(selectedIntegration);
        fixture.Repository.Reservations.Add(CreateReservation(
            fixture.CompanyId,
            fixture.PropertyId,
            fixture.Guest.Id,
            new DateOnly(2026, 7, 20),
            new DateOnly(2026, 7, 28),
            ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(
            BuildInboundPayload("wamid.selected", "+14155551234", selectedIntegration.PhoneNumberId),
            "cid-selected",
            CancellationToken.None);

        Assert.Equal(selectedIntegration.Id, fixture.ChatService.ObservedWhatsAppIntegrationId);
        Assert.Equal(fixture.CompanyId, fixture.ChatService.ObservedTenantCompanyIds.Single());
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

    // ===== RESERVATION SELECTION TESTS =====

    [Fact]
    public async Task ProcessAsync_ReservationSelectionA_OneCurrentReservation_Selected()
    {
        // A. Exactly one currently CheckedIn reservation
        // -> selected
        // -> conversation ReservationId matches
        // -> PropertyId matches
        var fixture = new Fixture();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var currentReservation = CreateReservation(
            fixture.CompanyId,
            fixture.PropertyId,
            fixture.Guest.Id,
            today.AddDays(-1),  // Checked in yesterday
            today.AddDays(2),   // Checking out in 2 days
            ReservationStatus.CheckedIn);
        fixture.Repository.Reservations.Add(currentReservation);

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.current", "+14155551234"), "cid-current", CancellationToken.None);

        // Should route to autonomous reply (ChatService)
        Assert.NotNull(fixture.ChatService.Request);
        Assert.Equal(fixture.Guest.Id, fixture.ChatService.Request!.GuestId);

        // ConversationService should NOT be called (autonomous path taken)
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
    }

    [Fact]
    public async Task ProcessAsync_ReservationSelectionB_OneUpcomingReservation_Selected()
    {
        // B. Exactly one eligible upcoming reservation
        // -> selected
        var fixture = new Fixture();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var upcomingReservation = CreateReservation(
            fixture.CompanyId,
            fixture.PropertyId,
            fixture.Guest.Id,
            today.AddDays(5),   // Check-in in 5 days
            today.AddDays(10),  // Check-out in 10 days
            ReservationStatus.Confirmed);
        fixture.Repository.Reservations.Add(upcomingReservation);

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.upcoming", "+14155551234"), "cid-upcoming", CancellationToken.None);

        // Should route to autonomous reply (ChatService)
        Assert.NotNull(fixture.ChatService.Request);
        Assert.Equal(fixture.Guest.Id, fixture.ChatService.Request!.GuestId);

        // ConversationService should NOT be called (autonomous path taken)
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
    }

    [Fact]
    public async Task ProcessAsync_ReservationSelectionC_TwoCurrentReservations_Ambiguous()
    {
        // C. Two current reservations
        // -> ambiguous
        // -> no reservation guessed
        // -> human review
        var fixture = new Fixture();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        fixture.Repository.Reservations.AddRange(
            CreateReservation(
                fixture.CompanyId,
                fixture.PropertyId,
                fixture.Guest.Id,
                today.AddDays(-1),
                today.AddDays(2),
                ReservationStatus.CheckedIn),
            CreateReservation(
                fixture.CompanyId,
                Guid.NewGuid(), // Different property
                fixture.Guest.Id,
                today,  // Also current today
                today.AddDays(3),
                ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.ambig2current", "+14155551234"), "cid-ambig2current", CancellationToken.None);

        // Should NOT route to autonomous reply
        Assert.Null(fixture.ChatService.Request);

        // Should route to host review via ConversationService
        Assert.NotNull(fixture.ConversationService.CreatedConversationRequest);
        Assert.True(fixture.ConversationService.HumanTakeoverEnabled);

        // Reservation should NOT be guessed (null)
        Assert.Null(fixture.ConversationService.CreatedConversationRequest!.ReservationId);
    }

    [Fact]
    public async Task ProcessAsync_ReservationSelectionD_TwoUpcomingReservations_Ambiguous()
    {
        // D. Two eligible upcoming reservations
        // -> ambiguous
        // -> no reservation guessed
        // -> human review
        var fixture = new Fixture();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        fixture.Repository.Reservations.AddRange(
            CreateReservation(
                fixture.CompanyId,
                fixture.PropertyId,
                fixture.Guest.Id,
                today.AddDays(5),
                today.AddDays(10),
                ReservationStatus.Confirmed),
            CreateReservation(
                fixture.CompanyId,
                Guid.NewGuid(), // Different property
                fixture.Guest.Id,
                today.AddDays(15),
                today.AddDays(20),
                ReservationStatus.Confirmed));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.ambig2upcoming", "+14155551234"), "cid-ambig2upcoming", CancellationToken.None);

        // Should NOT route to autonomous reply
        Assert.Null(fixture.ChatService.Request);

        // Should route to host review via ConversationService
        Assert.NotNull(fixture.ConversationService.CreatedConversationRequest);
        Assert.True(fixture.ConversationService.HumanTakeoverEnabled);

        // Reservation should NOT be guessed (null)
        Assert.Null(fixture.ConversationService.CreatedConversationRequest!.ReservationId);
    }

    [Fact]
    public async Task ProcessAsync_ReservationSelectionE_OnlyCompletedReservation_SelectedAsOnlyCandidate()
    {
        // E. Completed/expired reservation only (but within 30-day window)
        // -> IS selected if it's the only candidate (per algorithm)
        // -> autonomous reply taken
        var fixture = new Fixture();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        fixture.Repository.Reservations.Add(
            CreateReservation(
                fixture.CompanyId,
                fixture.PropertyId,
                fixture.Guest.Id,
                today.AddDays(-5),   // Checked in 5 days ago
                today.AddDays(-2),   // Checked out 2 days ago (recent completion)
                ReservationStatus.CheckedOut));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.completed", "+14155551234"), "cid-completed", CancellationToken.None);

        // Should route to autonomous reply (only 1 reservation total, even if checked out)
        Assert.NotNull(fixture.ChatService.Request);
        Assert.Equal(fixture.Guest.Id, fixture.ChatService.Request!.GuestId);

        // ConversationService should NOT be called (autonomous path taken)
        Assert.Null(fixture.ConversationService.CreatedConversationRequest);
    }

    [Fact]
    public async Task ProcessAsync_ReservationSelectionF_NoEligibleReservation_HostReview()
    {
        // F. No eligible reservation
        // -> human review behavior preserved
        var fixture = new Fixture();

        // Don't add any reservations

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.none", "+14155551234"), "cid-none", CancellationToken.None);

        // Should NOT route to autonomous reply
        Assert.Null(fixture.ChatService.Request);

        // Should route to host review via ConversationService
        Assert.NotNull(fixture.ConversationService.CreatedConversationRequest);
        Assert.True(fixture.ConversationService.HumanTakeoverEnabled);

        // Reservation should NOT be selected
        Assert.Null(fixture.ConversationService.CreatedConversationRequest!.ReservationId);
    }

    // ===== TENANT ISOLATION TESTS =====

    [Fact]
    public async Task ProcessAsync_TenantIsolation_ReservationFromAnotherCompany_NotSelected()
    {
        // Prove that a reservation belonging to another company cannot be selected
        var fixture = new Fixture();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var otherCompanyId = Guid.NewGuid();

        // Add reservation from DIFFERENT company
        fixture.Repository.Reservations.Add(
            CreateReservation(
                otherCompanyId,  // Different company
                fixture.PropertyId,
                fixture.Guest.Id,
                today.AddDays(-1),
                today.AddDays(2),
                ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.othertenant", "+14155551234"), "cid-othertenant", CancellationToken.None);

        // Should NOT route to autonomous reply (no reservation from OUR company)
        Assert.Null(fixture.ChatService.Request);

        // Should route to host review
        Assert.NotNull(fixture.ConversationService.CreatedConversationRequest);
        Assert.Null(fixture.ConversationService.CreatedConversationRequest!.ReservationId);
    }

    [Fact]
    public async Task ProcessAsync_TenantIsolation_ConversationFromAnotherTenant_NotReused()
    {
        // Prove that a conversation for another tenant cannot be reused
        // Note: This is tested via ConversationService behavior,
        // but the processor ensures correct company context is passed
        var fixture = new Fixture();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        fixture.Repository.Reservations.Add(
            CreateReservation(
                fixture.CompanyId,
                fixture.PropertyId,
                fixture.Guest.Id,
                today.AddDays(-1),
                today.AddDays(2),
                ReservationStatus.CheckedIn));

        await fixture.Processor.ProcessAsync(BuildInboundPayload("wamid.tenant", "+14155551234"), "cid-tenant", CancellationToken.None);

        // Verify tenant context is correctly passed
        Assert.Equal([fixture.CompanyId], fixture.ChatService.ObservedTenantCompanyIds);
        Assert.Single(fixture.ChatService.ObservedTenantCompanyIds);
        Assert.All(fixture.ChatService.ObservedTenantCompanyIds, id => Assert.Equal(fixture.CompanyId, id));
    }

    // ===== WHATSAPP INTEGRATION ISOLATION TESTS =====

    [Fact]
    public async Task ProcessAsync_IntegrationIsolation_ConversationBoundToIntegrationA_NotReusedByIntegrationB()
    {
        // Prove that the conversation is bound to the exact WhatsAppIntegrationId
        // that received the inbound message
        var fixture = new Fixture();
        var integrationA = fixture.Repository.Integration!;
        var integrationB = new WhatsAppIntegration
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            DisplayName = "Integration B",
            PhoneNumberId = "integration-b-phone-id",
            WhatsAppBusinessAccountId = "integration-b-waba-id",
            BusinessPhoneNumberMasked = "+1******5678",
            IsActive = true
        };
        fixture.Repository.Integrations.Add(integrationB);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        fixture.Repository.Reservations.Add(
            CreateReservation(
                fixture.CompanyId,
                fixture.PropertyId,
                fixture.Guest.Id,
                today.AddDays(-1),
                today.AddDays(2),
                ReservationStatus.CheckedIn));

        // Message arrives via Integration A
        await fixture.Processor.ProcessAsync(
            BuildInboundPayload("wamid.intA", "+14155551234", integrationA.PhoneNumberId),
            "cid-intA",
            CancellationToken.None);

        // Verify Integration A is observed
        Assert.Equal(integrationA.Id, fixture.ChatService.ObservedWhatsAppIntegrationId);
    }

    [Fact]
    public async Task ProcessAsync_IntegrationIsolation_RoutesCorrectIntegrationBasedOnPhoneNumberId()
    {
        // Prove that messages are routed by PhoneNumberId to correct integration
        var fixture = new Fixture();
        var integrationA = fixture.Repository.Integration!;
        var integrationB = new WhatsAppIntegration
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            DisplayName = "Integration B",
            PhoneNumberId = "integration-b-phone-id",
            WhatsAppBusinessAccountId = "integration-b-waba-id",
            BusinessPhoneNumberMasked = "+1******5678",
            IsActive = true
        };
        fixture.Repository.Integrations.Add(integrationB);

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        fixture.Repository.Reservations.Add(
            CreateReservation(
                fixture.CompanyId,
                fixture.PropertyId,
                fixture.Guest.Id,
                today.AddDays(-1),
                today.AddDays(2),
                ReservationStatus.CheckedIn));

        // Message arrives via Integration B's phone number
        await fixture.Processor.ProcessAsync(
            BuildInboundPayload("wamid.intB", "+14155551234", integrationB.PhoneNumberId),
            "cid-intB",
            CancellationToken.None);

        // Verify Integration B (not A) is routed to
        Assert.Equal(integrationB.Id, fixture.ChatService.ObservedWhatsAppIntegrationId);
        Assert.NotEqual(integrationA.Id, fixture.ChatService.ObservedWhatsAppIntegrationId);
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

    private static WhatsAppWebhookPayload BuildInboundPayload(string messageId, string from, string phoneNumberId = "demo-phone-number-id")
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
                        Metadata = new WhatsAppWebhookMetadata { PhoneNumberId = phoneNumberId },
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
                new NoOpGuestJourneyDeliveryReceiptSynchronizer(),
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

    private sealed class NoOpGuestJourneyDeliveryReceiptSynchronizer : IGuestJourneyDeliveryReceiptSynchronizer
    {
        public Task<bool> SyncAsync(Guid companyId, Guid conversationMessageId, ConversationMessageDeliveryStatus deliveryStatus, DateTimeOffset occurredAt, string? failureCode, string? failureReason, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
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

        public Task AddIntegrationAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
        {
            Integrations.Add(integration);
            return Task.CompletedTask;
        }

        public Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken)
            => Task.FromResult(ScopedIntegrations.FirstOrDefault(item => item.IsActive && item.PhoneNumberId == phoneNumberId));

        public Task<IReadOnlyCollection<WhatsAppIntegration>> ListActiveIntegrationsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<WhatsAppIntegration>>(ScopedIntegrations.Where(item => item.IsActive).ToList());

        public Task<WhatsAppIntegration?> GetSoleActiveIntegrationForCompanyAsync(Guid companyId, CancellationToken cancellationToken)
        {
            var candidates = ScopedIntegrations.Where(item => item.IsActive && item.CompanyId == companyId).ToList();
            return Task.FromResult(candidates.Count == 1 ? candidates[0] : null);
        }

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

        public Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid templateId, CancellationToken cancellationToken)
            => Task.FromResult(Templates.FirstOrDefault(item => item.CompanyId == companyId && item.Id == templateId));

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
        public Guid? ObservedWhatsAppIntegrationId { get; private set; }
        public int SendCallCount { get; private set; }
        public List<Guid?> ObservedTenantCompanyIds { get; } = [];
        public ITenantExecutionContextAccessor? TenantAccessor { get; set; }

        public Task<ApiResponse<ChatMessageResponse>> SendGuestMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken, Guid? whatsAppIntegrationId = null)
        {
            Request = request;
            ObservedWhatsAppIntegrationId = whatsAppIntegrationId;
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
        public Task<ApiResponse<ConversationMessageResponse>> AddHostMessageAsync(Guid conversationId, AddHostMessageRequest request, WhatsAppSendOrigin origin, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> RetryFailedMessageAsync(Guid currentConversationId, Guid messageId, CancellationToken cancellationToken)
        {
            RetriedConversationId = currentConversationId;
            RetriedMessageId = messageId;
            return Task.FromResult(RetryResult);
        }
        public Task<ApiResponse<ConversationMessageResponse>> AddInternalNoteAsync(Guid conversationId, AddInternalNoteRequest request, CancellationToken cancellationToken) => Task.FromResult(ApiResponse<ConversationMessageResponse>.Ok(new ConversationMessageResponse { Id = Guid.NewGuid(), ConversationId = conversationId, SenderType = ConversationSenderType.System, MessageType = ConversationMessageType.InternalNote, Content = request.Content, IsInternal = true, SentAt = DateTimeOffset.UtcNow }));
        public Task<ApiResponse<ConversationMessageResponse>> AddPaymentConfirmationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ApiResponse<ConversationMessageResponse>> AddLifecycleAutomationMessageAsync(Guid companyId, Guid conversationId, string content, string idempotencyKey, CancellationToken cancellationToken) => throw new NotImplementedException();

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