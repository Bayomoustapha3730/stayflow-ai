using System.Text.Json;
using StayFlow.Api.DTOs.Chat;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class WhatsAppWebhookProcessor(
    IWhatsAppRepository whatsAppRepository,
    IConversationRepository conversationRepository,
    IChatService chatService,
    IConversationService conversationService,
    IPhoneNumberNormalizer phoneNumberNormalizer,
    ITenantExecutionContextAccessor tenantExecutionContextAccessor,
    ILogger<WhatsAppWebhookProcessor> logger) : IWhatsAppWebhookProcessor
{
    private static readonly TimeSpan UpcomingReservationWindow = TimeSpan.FromDays(30);

    public async Task ProcessAsync(WhatsAppWebhookPayload payload, string correlationId, CancellationToken cancellationToken)
    {
        foreach (var entry in payload.Entry)
        {
            foreach (var change in entry.Changes)
            {
                try
                {
                    await ProcessChangeAsync(change, correlationId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "WhatsApp webhook change processing failed. CorrelationId={CorrelationId}", correlationId);
                }
            }
        }
    }

    private async Task ProcessChangeAsync(WhatsAppWebhookChange change, string correlationId, CancellationToken cancellationToken)
    {
        if (!string.Equals(change.Field, "messages", StringComparison.OrdinalIgnoreCase) || change.Value?.Metadata?.PhoneNumberId is not { Length: > 0 } phoneNumberId)
        {
            return;
        }

        var integration = await whatsAppRepository.GetActiveIntegrationByPhoneNumberIdAsync(phoneNumberId.Trim(), cancellationToken);
        if (integration is null)
        {
            return;
        }

        foreach (var message in change.Value.Messages)
        {
            await ProcessInboundMessageAsync(integration, message, correlationId, cancellationToken);
        }

        foreach (var status in change.Value.Statuses)
        {
            await ProcessStatusAsync(integration, status, correlationId, cancellationToken);
        }
    }

    private async Task ProcessInboundMessageAsync(WhatsAppIntegration integration, WhatsAppWebhookMessage message, string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.Id))
        {
            return;
        }

        var duplicate = await conversationRepository.FindByExternalMessageIdAsync(
            integration.CompanyId,
            message.Id.Trim(),
            ConversationMessageProvider.WhatsAppCloud,
            cancellationToken);
        if (duplicate is not null)
        {
            return;
        }

        if (!string.Equals(message.Type, "text", StringComparison.OrdinalIgnoreCase))
        {
            await RecordDiagnosticAsync(integration.CompanyId, "UnsupportedWhatsAppMessageType", new
            {
                correlationId,
                MessageType = message.Type,
                ExternalMessageId = message.Id
            }, cancellationToken);
            return;
        }

        if (!phoneNumberNormalizer.TryNormalize(message.From, out var normalizedPhone))
        {
            await RecordDiagnosticAsync(integration.CompanyId, "InvalidWhatsAppPhoneNumber", new
            {
                correlationId,
                ExternalMessageId = message.Id,
                Phone = phoneNumberNormalizer.Mask(message.From)
            }, cancellationToken);
            return;
        }

        var matchingGuests = (await whatsAppRepository.ListActiveGuestsWithPhoneAsync(integration.CompanyId, cancellationToken))
            .Where(guest => phoneNumberNormalizer.TryNormalize(guest.PhoneNumber, out var guestPhone) && string.Equals(guestPhone, normalizedPhone, StringComparison.Ordinal))
            .ToList();

        if (matchingGuests.Count != 1)
        {
            await RecordDiagnosticAsync(integration.CompanyId, matchingGuests.Count == 0 ? "WhatsAppGuestNotFound" : "WhatsAppGuestMatchAmbiguous", new
            {
                correlationId,
                ExternalMessageId = message.Id,
                Phone = phoneNumberNormalizer.Mask(normalizedPhone),
                MatchCount = matchingGuests.Count
            }, cancellationToken);
            return;
        }

        var guest = matchingGuests[0];
        var sentAt = ParseUnixTimestamp(message.Timestamp) ?? DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(sentAt.UtcDateTime);
        var reservations = await whatsAppRepository.GetEligibleReservationsForGuestAsync(
            integration.CompanyId,
            guest.Id,
            today,
            today.AddDays((int)UpcomingReservationWindow.TotalDays),
            cancellationToken);
        var resolution = ResolveReservation(reservations, sentAt);

        await ExecuteForCompanyAsync(integration.CompanyId, correlationId, async () =>
        {
            if (!resolution.AllowAutonomousReply)
            {
                var conversation = await conversationService.CreateOrGetConversationAsync(new CreateConversationRequest
                {
                    GuestId = guest.Id,
                    ReservationId = resolution.Reservation?.Id,
                    PropertyId = resolution.Reservation?.PropertyId,
                    Channel = GuestChannel.WhatsApp,
                    ChannelIdentity = normalizedPhone,
                    Subject = "WhatsApp guest support"
                }, cancellationToken);

                if (!conversation.Success || conversation.Data is null)
                {
                    return;
                }

                await conversationService.AddGuestMessageAsync(conversation.Data.ConversationId, new AddGuestMessageRequest
                {
                    Content = message.Text?.Body ?? string.Empty,
                    ExternalMessageId = message.Id.Trim(),
                    Provider = ConversationMessageProvider.WhatsAppCloud,
                    SentAt = sentAt
                }, cancellationToken);
                await conversationService.EnableHumanTakeoverAsync(conversation.Data.ConversationId, cancellationToken);
                await conversationService.AddInternalNoteAsync(conversation.Data.ConversationId, new AddInternalNoteRequest
                {
                    Content = resolution.HostAttentionReason
                }, cancellationToken);
                return;
            }

            await chatService.SendGuestMessageAsync(new SendChatMessageRequest
            {
                GuestId = guest.Id,
                ReservationId = resolution.Reservation?.Id,
                PropertyId = resolution.Reservation?.PropertyId,
                Message = message.Text?.Body ?? string.Empty,
                Channel = GuestChannel.WhatsApp,
                ChannelIdentity = normalizedPhone,
                ExternalMessageId = message.Id.Trim(),
                CurrentTimestamp = sentAt
            }, cancellationToken);
        });
    }

    private async Task ProcessStatusAsync(WhatsAppIntegration integration, WhatsAppWebhookStatus status, string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(status.Id))
        {
            return;
        }

        var message = await whatsAppRepository.FindMessageByProviderExternalIdAsync(
            integration.CompanyId,
            ConversationMessageProvider.WhatsAppCloud,
            status.Id.Trim(),
            cancellationToken);
        if (message is null)
        {
            await RecordDiagnosticAsync(integration.CompanyId, "WhatsAppStatusTargetNotFound", new
            {
                correlationId,
                ExternalMessageId = status.Id
            }, cancellationToken);
            return;
        }

        if (!TryMapStatus(status, out var deliveryStatus, out var failureCode, out var failureReason))
        {
            return;
        }

        var occurredAt = ParseUnixTimestamp(status.Timestamp) ?? DateTimeOffset.UtcNow;
        await ExecuteForCompanyAsync(integration.CompanyId, correlationId, async () =>
        {
            await conversationService.UpdateMessageDeliveryStatusAsync(
                message.ConversationId,
                message.Id,
                deliveryStatus,
                occurredAt,
                failureCode,
                failureReason,
                cancellationToken);
        });
    }

    private async Task ExecuteForCompanyAsync(Guid companyId, string correlationId, Func<Task> action)
    {
        tenantExecutionContextAccessor.Set(companyId, null, correlationId);
        try
        {
            await action();
        }
        finally
        {
            tenantExecutionContextAccessor.Clear();
        }
    }

    private async Task RecordDiagnosticAsync(Guid companyId, string action, object details, CancellationToken cancellationToken)
    {
        await whatsAppRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "WhatsAppWebhook",
            EntityId = companyId,
            Action = action,
            Details = JsonSerializer.Serialize(details),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await whatsAppRepository.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset? ParseUnixTimestamp(string? raw)
    {
        return long.TryParse(raw, out var unixTime)
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime)
            : null;
    }

    private static ReservationResolution ResolveReservation(IReadOnlyCollection<Reservation> reservations, DateTimeOffset sentAt)
    {
        if (reservations.Count == 0)
        {
            return new ReservationResolution(null, false, "WhatsApp message requires host review because no active reservation was resolved.");
        }

        var currentDate = DateOnly.FromDateTime(sentAt.UtcDateTime);
        var current = reservations
            .Where(reservation => reservation.CheckInDate <= currentDate && reservation.CheckOutDate >= currentDate)
            .ToList();

        if (current.Count == 1)
        {
            return new ReservationResolution(current[0], true, string.Empty);
        }

        if (current.Count > 1)
        {
            return new ReservationResolution(null, false, "WhatsApp message requires host review because multiple current reservations matched this guest.");
        }

        if (reservations.Count == 1)
        {
            return new ReservationResolution(reservations.First(), true, string.Empty);
        }

        return new ReservationResolution(null, false, "WhatsApp message requires host review because multiple reservation candidates matched this guest.");
    }

    private static bool TryMapStatus(WhatsAppWebhookStatus status, out ConversationMessageDeliveryStatus deliveryStatus, out string? failureCode, out string? failureReason)
    {
        failureCode = null;
        failureReason = null;
        switch (status.Status?.Trim().ToLowerInvariant())
        {
            case "sent":
                deliveryStatus = ConversationMessageDeliveryStatus.Sent;
                return true;
            case "delivered":
                deliveryStatus = ConversationMessageDeliveryStatus.Delivered;
                return true;
            case "read":
                deliveryStatus = ConversationMessageDeliveryStatus.Read;
                return true;
            case "failed":
                deliveryStatus = ConversationMessageDeliveryStatus.Failed;
                var error = status.Errors.FirstOrDefault();
                failureCode = error?.Code?.ToString();
                failureReason = error?.Title ?? error?.Message;
                return true;
            default:
                deliveryStatus = default;
                return false;
        }
    }

    private sealed record ReservationResolution(Reservation? Reservation, bool AllowAutonomousReply, string HostAttentionReason);
}