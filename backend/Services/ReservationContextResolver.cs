using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class ReservationContextResolver(
    IReservationRepository reservationRepository,
    ICurrentTenantContext currentTenantContext,
    IOptions<ReservationContextOptions> options,
    ILogger<ReservationContextResolver> logger) : IReservationContextResolver
{
    public async Task<ReservationContextResolutionResult> ResolveAsync(ReservationContextRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Reservation context request accepted. CorrelationId={CorrelationId} HasGuestId={HasGuestId} HasConversationId={HasConversationId} Categories={Categories}",
            currentTenantContext.CorrelationId,
            request.GuestId.HasValue,
            request.ConversationId.HasValue,
            request.QuestionCategories.Select(category => category.ToString()).ToArray());

        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            var failure = Escalation(tenantError, null, "TenantContextUnavailable");
            await AddAuditLogAsync(companyId: null, request.GuestId, 0, failure, cancellationToken);
            await reservationRepository.SaveChangesAsync(cancellationToken);
            return failure;
        }

        var currentDate = DateOnly.FromDateTime(request.CurrentTimestamp.UtcDateTime);
        var upcomingThroughDate = currentDate.AddDays(options.Value.PreArrivalWindowDays);
        Conversation? conversation = null;
        Guid? guestId = request.GuestId;

        if (request.ConversationId is { } conversationId)
        {
            conversation = await reservationRepository.GetConversationAsync(conversationId, companyId, cancellationToken);
            if (conversation is null)
            {
                var result = Escalation("Conversation was not found or is outside the authenticated tenant.", guestId, "ConversationTenantMismatch");
                await AddAuditLogAsync(companyId, guestId, 0, result, cancellationToken);
                await reservationRepository.SaveChangesAsync(cancellationToken);
                return result;
            }

            if (guestId is { } requestedGuestId && requestedGuestId != conversation.GuestId)
            {
                var result = Escalation("Conversation guest identity conflicts with the supplied guest identity.", requestedGuestId, "ConflictingGuestIdentity");
                await AddAuditLogAsync(companyId, requestedGuestId, 0, result, cancellationToken);
                await reservationRepository.SaveChangesAsync(cancellationToken);
                return result;
            }

            guestId ??= conversation.GuestId;

        }

        if (guestId is null || guestId == Guid.Empty)
        {
            var result = Escalation("A tenant-scoped guest identity is required before reservation context resolution.", guestId, "MissingGuestIdentity");
            await AddAuditLogAsync(companyId, guestId, 0, result, cancellationToken);
            await reservationRepository.SaveChangesAsync(cancellationToken);
            return result;
        }

        var guest = await reservationRepository.GetGuestAsync(guestId.Value, companyId, cancellationToken);
        if (guest is null)
        {
            var result = Escalation("Guest identity could not be verified within the authenticated tenant.", guestId, "GuestTenantMismatch");
            await AddAuditLogAsync(companyId, guestId, 0, result, cancellationToken);
            await reservationRepository.SaveChangesAsync(cancellationToken);
            return result;
        }

        logger.LogInformation(
            "Reservation context guest resolved. CorrelationId={CorrelationId} GuestId={GuestId}",
            currentTenantContext.CorrelationId,
            guest.Id);

        var channelValidation = ValidateChannelIdentity(request, guest);
        if (!channelValidation.IsValid)
        {
            var result = Escalation(channelValidation.Message, guest.Id, channelValidation.EscalationReason);
            await AddAuditLogAsync(companyId, guest.Id, 0, result, cancellationToken);
            await reservationRepository.SaveChangesAsync(cancellationToken);
            return result;
        }

        if (conversation?.Reservation is not null
            && conversation.Reservation.PrimaryGuestId == guest.Id
            && IsEligible(conversation.Reservation, currentDate, upcomingThroughDate))
        {
            var result = ReservationContextResolutionResult.Resolved(
                companyId,
                conversation.GuestId,
                conversation.Reservation.Id,
                conversation.Reservation.PropertyId,
                ReservationContextResolutionMethod.VerifiedConversationBinding,
                request.CurrentTimestamp);

            await AddAuditLogAsync(companyId, conversation.GuestId, 1, result, cancellationToken);
            await reservationRepository.SaveChangesAsync(cancellationToken);
            return result;
        }

        if (!string.IsNullOrWhiteSpace(request.ExplicitReservationReference))
        {
            var referenceCandidates = (await reservationRepository.GetEligibleReservationsByReferenceAsync(
                    companyId,
                    guest.Id,
                    NormalizeReference(request.ExplicitReservationReference),
                    cancellationToken))
                .Where(reservation => IsEligible(reservation, currentDate, upcomingThroughDate))
                .ToList();

            var result = ResolveCandidates(referenceCandidates, companyId, guest.Id, ReservationContextResolutionMethod.ExplicitReservationReference, request.CurrentTimestamp);
            await PersistBindingAndAuditAsync(conversation, result, referenceCandidates.Count, cancellationToken);
            return result;
        }

        var candidates = (await reservationRepository.GetEligibleReservationsForGuestAsync(
                companyId,
                guest.Id,
                currentDate,
                upcomingThroughDate,
                cancellationToken))
            .ToList();
        logger.LogInformation(
            "Reservation context eligible candidates loaded. CorrelationId={CorrelationId} CandidateCount={CandidateCount} CurrentDate={CurrentDate} UpcomingThroughDate={UpcomingThroughDate}",
            currentTenantContext.CorrelationId,
            candidates.Count,
            currentDate,
            upcomingThroughDate);

        if (!string.IsNullOrWhiteSpace(request.ExplicitPropertyName))
        {
            candidates = (await reservationRepository.GetEligibleReservationsByPropertyNameAsync(
                    companyId,
                    guest.Id,
                    currentDate,
                    upcomingThroughDate,
                    request.ExplicitPropertyName.Trim(),
                    cancellationToken))
                .ToList();

            var propertyResult = ResolveCandidates(candidates, companyId, guest.Id, ReservationContextResolutionMethod.ExplicitPropertyName, request.CurrentTimestamp);
            if (propertyResult.Outcome != ReservationContextResolutionOutcome.NoEligibleReservation)
            {
                await PersistBindingAndAuditAsync(conversation, propertyResult, candidates.Count, cancellationToken);
                return propertyResult;
            }
        }

        var activeCandidates = candidates.Where(reservation => IsActiveReservation(reservation, currentDate)).ToList();
        if (activeCandidates.Count == 1)
        {
            var result = ToResolved(activeCandidates[0], companyId, guest.Id, ReservationContextResolutionMethod.SingleActiveReservation, request.CurrentTimestamp);
            await PersistBindingAndAuditAsync(conversation, result, activeCandidates.Count, cancellationToken);
            return result;
        }

        if (activeCandidates.Count > 1)
        {
            var result = Clarification(activeCandidates, companyId, guest.Id);
            await PersistBindingAndAuditAsync(conversation, result, activeCandidates.Count, cancellationToken);
            return result;
        }

        var upcomingCandidates = candidates.Where(reservation => IsUpcomingReservation(reservation, currentDate, upcomingThroughDate)).ToList();
        if (upcomingCandidates.Count == 1)
        {
            var result = ToResolved(upcomingCandidates[0], companyId, guest.Id, ReservationContextResolutionMethod.SingleUpcomingReservation, request.CurrentTimestamp);
            await PersistBindingAndAuditAsync(conversation, result, upcomingCandidates.Count, cancellationToken);
            return result;
        }

        if (IsReservationDateQuestion(request.QuestionCategories))
        {
            var futureCandidates = (await reservationRepository.GetFutureReservationsForGuestAsync(companyId, guest.Id, currentDate, cancellationToken)).ToList();
            logger.LogInformation(
                "Reservation context future date candidates loaded. CorrelationId={CorrelationId} CandidateCount={CandidateCount}",
                currentTenantContext.CorrelationId,
                futureCandidates.Count);

            if (futureCandidates.Count == 1)
            {
                var result = ToResolved(futureCandidates[0], companyId, guest.Id, ReservationContextResolutionMethod.SingleFutureReservationForDateQuestion, request.CurrentTimestamp);
                await PersistBindingAndAuditAsync(conversation, result, futureCandidates.Count, cancellationToken);
                return result;
            }

            if (futureCandidates.Count > 1)
            {
                var result = Clarification(futureCandidates, companyId, guest.Id);
                await PersistBindingAndAuditAsync(conversation, result, futureCandidates.Count, cancellationToken);
                return result;
            }
        }

        var finalResult = upcomingCandidates.Count > 1
            ? Clarification(upcomingCandidates, companyId, guest.Id)
            : new ReservationContextResolutionResult
            {
                Outcome = ReservationContextResolutionOutcome.NoEligibleReservation,
                CompanyId = companyId,
                GuestId = guest.Id,
                Message = "No eligible reservation was found."
            };

        await PersistBindingAndAuditAsync(conversation, finalResult, upcomingCandidates.Count, cancellationToken);
        return finalResult;
    }

    private async Task PersistBindingAndAuditAsync(Conversation? conversation, ReservationContextResolutionResult result, int candidateCount, CancellationToken cancellationToken)
    {
        if (conversation is not null && result.Outcome == ReservationContextResolutionOutcome.Resolved && result.ReservationId is { } reservationId)
        {
            conversation.ReservationId = reservationId;
            conversation.ReservationContextBoundAt = result.ResolvedAt;
            conversation.ReservationContextResolutionMethod = result.ResolutionMethod?.ToString();
        }

        logger.LogInformation(
            "Reservation context resolution completed. CorrelationId={CorrelationId} Outcome={Outcome} ResolutionMethod={ResolutionMethod} CandidateCount={CandidateCount} EscalationReason={EscalationReason}",
            currentTenantContext.CorrelationId,
            result.Outcome,
            result.ResolutionMethod,
            candidateCount,
            result.EscalationReason);
        await AddAuditLogAsync(result.CompanyId, result.GuestId, candidateCount, result, cancellationToken);
        await reservationRepository.SaveChangesAsync(cancellationToken);
    }

    private ReservationContextResolutionResult ResolveCandidates(
        IReadOnlyCollection<Reservation> candidates,
        Guid companyId,
        Guid guestId,
        ReservationContextResolutionMethod method,
        DateTimeOffset resolvedAt)
    {
        return candidates.Count switch
        {
            0 => new ReservationContextResolutionResult
            {
                Outcome = ReservationContextResolutionOutcome.NoEligibleReservation,
                CompanyId = companyId,
                GuestId = guestId,
                Message = "No eligible reservation was found."
            },
            1 => ToResolved(candidates.Single(), companyId, guestId, method, resolvedAt),
            _ => Clarification(candidates, companyId, guestId)
        };
    }

    private static ReservationContextResolutionResult ToResolved(Reservation reservation, Guid companyId, Guid guestId, ReservationContextResolutionMethod method, DateTimeOffset resolvedAt)
    {
        return ReservationContextResolutionResult.Resolved(companyId, guestId, reservation.Id, reservation.PropertyId, method, resolvedAt);
    }

    private static ReservationContextResolutionResult Clarification(IEnumerable<Reservation> candidates, Guid companyId, Guid guestId)
    {
        var candidateLabels = candidates
            .OrderBy(reservation => reservation.CheckInDate)
            .Select(reservation => new ReservationCandidateLabel
            {
                PropertyName = reservation.Property.Name,
                City = reservation.Property.City,
                CheckInDate = reservation.CheckInDate
            })
            .ToList();

        return new ReservationContextResolutionResult
        {
            Outcome = ReservationContextResolutionOutcome.ClarificationRequired,
            CompanyId = companyId,
            GuestId = guestId,
            CandidateLabels = candidateLabels,
            Message = "Multiple eligible reservations require clarification."
        };
    }

    private static ReservationContextResolutionResult Escalation(string message, Guid? guestId, string escalationReason)
    {
        return new ReservationContextResolutionResult
        {
            Outcome = ReservationContextResolutionOutcome.EscalationRequired,
            GuestId = guestId,
            EscalationReason = escalationReason,
            Message = message
        };
    }

    private async Task AddAuditLogAsync(Guid? companyId, Guid? guestId, int candidateCount, ReservationContextResolutionResult result, CancellationToken cancellationToken)
    {
        await reservationRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "ReservationContext",
            EntityId = result.ReservationId ?? Guid.Empty,
            Action = "Resolved",
            Details = JsonSerializer.Serialize(new
            {
                currentTenantContext.CorrelationId,
                CompanyId = companyId,
                GuestCandidateId = guestId,
                CandidateReservationCount = candidateCount,
                Outcome = result.Outcome.ToString(),
                ResolutionMethod = result.ResolutionMethod?.ToString(),
                SelectedReservationId = result.ReservationId,
                ClarificationRequested = result.Outcome == ReservationContextResolutionOutcome.ClarificationRequired,
                result.EscalationReason
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        if (!currentTenantContext.IsAuthenticated)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        if (currentTenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is missing or invalid.";
            return false;
        }

        companyId = tenantCompanyId;
        error = string.Empty;
        return true;
    }

    private static bool IsEligible(Reservation reservation, DateOnly currentDate, DateOnly upcomingThroughDate)
    {
        return !reservation.IsDeleted
            && reservation.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow
            && (IsActiveReservation(reservation, currentDate) || IsUpcomingReservation(reservation, currentDate, upcomingThroughDate));
    }

    private static bool IsActiveReservation(Reservation reservation, DateOnly currentDate)
    {
        return reservation.Status is ReservationStatus.ReadyForCheckIn or ReservationStatus.CheckedIn or ReservationStatus.ActiveStay or ReservationStatus.CheckOutPending
            && reservation.CheckInDate <= currentDate
            && reservation.CheckOutDate >= currentDate;
    }

    private static bool IsUpcomingReservation(Reservation reservation, DateOnly currentDate, DateOnly upcomingThroughDate)
    {
        return reservation.Status is ReservationStatus.Confirmed or ReservationStatus.PreArrival or ReservationStatus.ReadyForCheckIn
            && reservation.CheckInDate >= currentDate
            && reservation.CheckInDate <= upcomingThroughDate;
    }

    private static string NormalizeReference(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static string NormalizePhone(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string NormalizeEmail(string value)
    {
        return value.Trim().ToUpperInvariant();
    }

    private static ChannelIdentityValidationResult ValidateChannelIdentity(ReservationContextRequest request, Guest guest)
    {
        var hasChannel = !string.IsNullOrWhiteSpace(request.Channel);
        var hasChannelIdentity = !string.IsNullOrWhiteSpace(request.ChannelIdentity);

        if (!hasChannel)
        {
            return hasChannelIdentity
                ? ChannelIdentityValidationResult.Fail("UnsupportedChannel", "A supported guest communication channel is required when channel identity is supplied.")
                : ChannelIdentityValidationResult.Success();
        }

        if (!TryParseChannel(request.Channel, out var channel))
        {
            return ChannelIdentityValidationResult.Fail("UnsupportedChannel", "The supplied guest communication channel is not supported.");
        }

        return channel switch
        {
            GuestChannel.WhatsApp or GuestChannel.SMS => ValidatePhoneChannelIdentity(request.ChannelIdentity, guest.PhoneNumber),
            GuestChannel.Email => ValidateEmailChannelIdentity(request.ChannelIdentity, guest.Email),
            GuestChannel.Web => ValidateWebChannelIdentity(request.ChannelIdentity, guest),
            _ => ChannelIdentityValidationResult.Fail("UnsupportedChannel", "The supplied guest communication channel is not supported.")
        };
    }

    private static bool TryParseChannel(string? value, out GuestChannel channel)
    {
        channel = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        if (string.Equals(normalized, "Whatsapp", StringComparison.OrdinalIgnoreCase))
        {
            channel = GuestChannel.WhatsApp;
            return true;
        }

        return Enum.TryParse(normalized, ignoreCase: true, out channel)
            && Enum.IsDefined(channel);
    }

    private static ChannelIdentityValidationResult ValidatePhoneChannelIdentity(string? channelIdentity, string? guestPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(channelIdentity) || string.IsNullOrWhiteSpace(guestPhoneNumber))
        {
            return ChannelIdentityValidationResult.Fail("MissingChannelIdentity", "Phone channel identity is required to verify the resolved guest.");
        }

        return string.Equals(NormalizePhone(channelIdentity), NormalizePhone(guestPhoneNumber), StringComparison.Ordinal)
            ? ChannelIdentityValidationResult.Success()
            : ChannelIdentityValidationResult.Fail("ConflictingChannelIdentity", "Channel identity conflicts with the resolved guest identity.");
    }

    private static ChannelIdentityValidationResult ValidateEmailChannelIdentity(string? channelIdentity, string? guestEmail)
    {
        if (string.IsNullOrWhiteSpace(channelIdentity) || string.IsNullOrWhiteSpace(guestEmail))
        {
            return ChannelIdentityValidationResult.Fail("MissingChannelIdentity", "Email channel identity is required to verify the resolved guest.");
        }

        return string.Equals(NormalizeEmail(channelIdentity), NormalizeEmail(guestEmail), StringComparison.Ordinal)
            ? ChannelIdentityValidationResult.Success()
            : ChannelIdentityValidationResult.Fail("ConflictingChannelIdentity", "Channel identity conflicts with the resolved guest identity.");
    }

    private static ChannelIdentityValidationResult ValidateWebChannelIdentity(string? channelIdentity, Guest guest)
    {
        if (string.IsNullOrWhiteSpace(channelIdentity))
        {
            return ChannelIdentityValidationResult.Success();
        }

        var trimmedIdentity = channelIdentity.Trim();
        if (trimmedIdentity.StartsWith("email:", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateEmailChannelIdentity(trimmedIdentity["email:".Length..], guest.Email);
        }

        if (trimmedIdentity.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateEmailChannelIdentity(trimmedIdentity["mailto:".Length..], guest.Email);
        }

        if (trimmedIdentity.StartsWith("phone:", StringComparison.OrdinalIgnoreCase))
        {
            return ValidatePhoneChannelIdentity(trimmedIdentity["phone:".Length..], guest.PhoneNumber);
        }

        return ChannelIdentityValidationResult.Fail("UnsupportedWebIdentityType", "The supplied web channel identity type is not supported for guest verification.");
    }

    private static bool IsReservationDateQuestion(IReadOnlyCollection<QuestionContextCategory> categories)
    {
        return categories.Contains(QuestionContextCategory.CheckIn)
            || categories.Contains(QuestionContextCategory.CheckOut);
    }

    private readonly record struct ChannelIdentityValidationResult(bool IsValid, string EscalationReason, string Message)
    {
        public static ChannelIdentityValidationResult Success()
        {
            return new ChannelIdentityValidationResult(true, string.Empty, string.Empty);
        }

        public static ChannelIdentityValidationResult Fail(string escalationReason, string message)
        {
            return new ChannelIdentityValidationResult(false, escalationReason, message);
        }
    }
}
