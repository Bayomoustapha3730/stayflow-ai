using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class AIContextBuilder(
    IAIContextRepository aiContextRepository,
    IReservationContextResolver reservationContextResolver,
    IQuestionRelevanceClassifier questionRelevanceClassifier,
    ICurrentTenantContext currentTenantContext,
    IOptions<AIContextOptions> options,
    ILogger<AIContextBuilder> logger) : IAIContextBuilder
{
    public async Task<AIContextBuildResult> BuildAsync(AIContextRequest request, CancellationToken cancellationToken)
    {
        var categories = questionRelevanceClassifier.Classify(request.GuestQuestion);
        logger.LogInformation(
            "AI context request accepted. CorrelationId={CorrelationId} HasGuestId={HasGuestId} HasConversationId={HasConversationId}",
            currentTenantContext.CorrelationId,
            request.GuestId.HasValue,
            request.ConversationId.HasValue);
        logger.LogInformation(
            "AI question categories classified. CorrelationId={CorrelationId} Categories={Categories}",
            currentTenantContext.CorrelationId,
            categories.Select(category => category.ToString()).ToArray());

        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            var tenantFailure = new AIContextBuildResult
            {
                Outcome = AIContextBuildOutcome.EscalationRequired,
                QuestionCategories = categories,
                Metadata = new AIContextBuildMetadata { GuestId = request.GuestId },
                EscalationReason = "TenantContextUnavailable",
                Message = tenantError
            };

            await AuditAsync(null, request.GuestId, null, "NotResolved", categories, tenantFailure, 0, 0, cancellationToken);
            return tenantFailure;
        }

        var reservationContext = await reservationContextResolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = request.GuestId,
            ConversationId = request.ConversationId,
            Channel = request.Channel,
            ChannelIdentity = request.ChannelIdentity,
            ExplicitReservationReference = request.ExplicitReservationReference,
            ExplicitPropertyName = request.ExplicitPropertyName,
            QuestionCategories = categories,
            CurrentTimestamp = request.CurrentTimestamp
        }, cancellationToken);

        logger.LogInformation(
            "AI reservation context resolved. CorrelationId={CorrelationId} Outcome={Outcome} GuestResolved={GuestResolved} ReservationResolved={ReservationResolved} PropertyResolved={PropertyResolved} CandidateCount={CandidateCount} EscalationReason={EscalationReason}",
            currentTenantContext.CorrelationId,
            reservationContext.Outcome,
            reservationContext.GuestId.HasValue,
            reservationContext.ReservationId.HasValue,
            reservationContext.PropertyId.HasValue,
            reservationContext.CandidateLabels.Count,
            reservationContext.EscalationReason);

        var result = reservationContext.Outcome switch
        {
            ReservationContextResolutionOutcome.ClarificationRequired => ClarificationResult(reservationContext, categories),
            ReservationContextResolutionOutcome.EscalationRequired => EscalationResult(reservationContext, categories),
            ReservationContextResolutionOutcome.NoEligibleReservation => await NoEligibleReservationResultAsync(companyId, reservationContext, categories, cancellationToken),
            ReservationContextResolutionOutcome.Resolved => await ReadyResultAsync(companyId, reservationContext, request, categories, cancellationToken),
            _ => new AIContextBuildResult
            {
                Outcome = AIContextBuildOutcome.EscalationRequired,
                QuestionCategories = categories,
                Metadata = MetadataFromReservationContext(reservationContext, companyId),
                EscalationReason = "UnsupportedReservationContextOutcome",
                Message = "Reservation context resolution returned an unsupported outcome."
            }
        };
        result.ReservationContextOutcome = reservationContext.Outcome.ToString();
        result.ReservationContextMessage = reservationContext.Message;

        var knowledgeCount = result.Context?.Knowledge.Articles.Count ?? 0;
        var recommendationCount = result.Context?.Knowledge.Recommendations.Count ?? 0;
        logger.LogInformation(
            "AI context build completed. CorrelationId={CorrelationId} Outcome={Outcome} KnowledgeArticleCount={KnowledgeArticleCount} RecommendationCount={RecommendationCount} EscalationReason={EscalationReason}",
            currentTenantContext.CorrelationId,
            result.Outcome,
            knowledgeCount,
            recommendationCount,
            result.EscalationReason);
        await AuditAsync(companyId, reservationContext.GuestId, reservationContext.ReservationId, reservationContext.Outcome.ToString(), categories, result, knowledgeCount, recommendationCount, cancellationToken);
        return result;
    }

    private async Task<AIContextBuildResult> ReadyResultAsync(
        Guid companyId,
        ReservationContextResolutionResult reservationContext,
        AIContextRequest request,
        IReadOnlyCollection<QuestionContextCategory> categories,
        CancellationToken cancellationToken)
    {
        if (reservationContext.GuestId is not { } guestId || reservationContext.ReservationId is not { } reservationId || reservationContext.PropertyId is not { } propertyId)
        {
            return new AIContextBuildResult
            {
                Outcome = AIContextBuildOutcome.EscalationRequired,
                QuestionCategories = categories,
                Metadata = new AIContextBuildMetadata { CompanyId = companyId },
                EscalationReason = "IncompleteResolvedReservationContext",
                Message = "Resolved reservation context is missing required identifiers."
            };
        }

        var guest = await aiContextRepository.GetGuestAsync(companyId, guestId, cancellationToken);
        var reservation = await aiContextRepository.GetReservationAsync(companyId, reservationId, cancellationToken);
        var property = await aiContextRepository.GetPropertyContextAsync(companyId, propertyId, cancellationToken);

        if (guest is null || reservation is null || property is null || reservation.PrimaryGuestId != guest.Id || reservation.PropertyId != property.Id)
        {
            return new AIContextBuildResult
            {
                Outcome = AIContextBuildOutcome.EscalationRequired,
                QuestionCategories = categories,
                Metadata = new AIContextBuildMetadata { CompanyId = companyId },
                EscalationReason = "ResolvedContextValidationFailed",
                Message = "Resolved reservation context could not be validated within the authenticated tenant."
            };
        }

        Conversation? conversation = null;
        if (request.ConversationId is { } conversationId)
        {
            conversation = await aiContextRepository.GetConversationAsync(companyId, conversationId, cancellationToken);
            if (conversation is null || conversation.GuestId != guest.Id || conversation.PropertyId != property.Id || (conversation.ReservationId is { } boundReservationId && boundReservationId != reservation.Id))
            {
                return new AIContextBuildResult
                {
                    Outcome = AIContextBuildOutcome.EscalationRequired,
                    QuestionCategories = categories,
                    Metadata = new AIContextBuildMetadata
                    {
                        CompanyId = companyId,
                        GuestId = guest.Id,
                        ReservationId = reservation.Id,
                        PropertyId = property.Id
                    },
                    EscalationReason = "ConversationContextValidationFailed",
                    Message = "Conversation context could not be validated within the authenticated tenant."
                };
            }
        }

        var returningGuest = await aiContextRepository.CountCompletedReservationsForGuestAsync(companyId, guest.Id, cancellationToken) > 0;
        var requiresAccessAuthorization = categories.Contains(QuestionContextCategory.PropertyAccess);
        var knowledge = BuildKnowledgeContext(property, categories, requiresAccessAuthorization);

        return new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.Ready,
            QuestionCategories = categories,
            Metadata = new AIContextBuildMetadata
            {
                CompanyId = companyId,
                GuestId = guest.Id,
                ReservationId = reservation.Id,
                PropertyId = property.Id
            },
            Context = new AIContext
            {
                Guest = new AIGuestContext
                {
                    PreferredLanguage = guest.PreferredLanguage,
                    IsReturningGuest = returningGuest
                },
                Reservation = new AIReservationContext
                {
                    Status = reservation.Status.ToString(),
                    CheckInDate = reservation.CheckInDate,
                    CheckOutDate = reservation.CheckOutDate,
                    CurrentStayPhase = DetermineStayPhase(reservation, request.CurrentTimestamp),
                    Adults = reservation.Adults,
                    Children = reservation.Children,
                    SpecialRequests = string.IsNullOrWhiteSpace(reservation.SpecialRequests) ? null : reservation.SpecialRequests.Trim()
                },
                Property = new AIPropertyContext
                {
                    DisplayName = property.Name,
                    City = property.City,
                    CountryCode = property.CountryCode,
                    TimeZone = property.TimeZone,
                    Description = property.Description
                },
                Knowledge = knowledge,
                Conversation = conversation is null
                    ? null
                    : new AIConversationContext
                    {
                        Channel = conversation.Channel,
                        Status = conversation.Status,
                        HasVerifiedReservationBinding = conversation.ReservationId == reservation.Id
                    },
                Safety = new AISafetyContext
                {
                    RequiresPropertyAccessAuthorization = requiresAccessAuthorization,
                    ReservationContextResolved = true,
                    TenantValidated = true,
                    GuestValidated = true,
                    ContextMinimized = true
                }
            }
        };
    }

    private async Task<AIContextBuildResult> NoEligibleReservationResultAsync(
        Guid companyId,
        ReservationContextResolutionResult reservationContext,
        IReadOnlyCollection<QuestionContextCategory> categories,
        CancellationToken cancellationToken)
    {
        AIGuestContext? guestContext = null;
        if (reservationContext.GuestId is { } guestId)
        {
            var guest = await aiContextRepository.GetGuestAsync(companyId, guestId, cancellationToken);
            if (guest is not null)
            {
                guestContext = new AIGuestContext { PreferredLanguage = guest.PreferredLanguage };
            }
        }

        return new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.NoEligibleReservation,
            QuestionCategories = categories,
            Metadata = new AIContextBuildMetadata
            {
                CompanyId = companyId,
                GuestId = guestContext is not null ? reservationContext.GuestId : null
            },
            Message = reservationContext.Message,
            Context = new AIContext
            {
                Guest = guestContext,
                Safety = new AISafetyContext
                {
                    RequiresPropertyAccessAuthorization = categories.Contains(QuestionContextCategory.PropertyAccess),
                    ReservationContextResolved = false,
                    TenantValidated = true,
                    GuestValidated = guestContext is not null,
                    ContextMinimized = true
                }
            }
        };
    }

    private static AIContextBuildResult ClarificationResult(ReservationContextResolutionResult reservationContext, IReadOnlyCollection<QuestionContextCategory> categories)
    {
        return new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.ClarificationRequired,
            CandidateLabels = reservationContext.CandidateLabels,
            QuestionCategories = categories,
            Metadata = MetadataFromReservationContext(reservationContext),
            Message = reservationContext.Message
        };
    }

    private static AIContextBuildResult EscalationResult(ReservationContextResolutionResult reservationContext, IReadOnlyCollection<QuestionContextCategory> categories)
    {
        return new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.EscalationRequired,
            QuestionCategories = categories,
            Metadata = MetadataFromReservationContext(reservationContext),
            EscalationReason = reservationContext.EscalationReason,
            Message = reservationContext.Message
        };
    }

    private static AIContextBuildMetadata MetadataFromReservationContext(ReservationContextResolutionResult reservationContext, Guid? fallbackCompanyId = null)
    {
        return new AIContextBuildMetadata
        {
            CompanyId = reservationContext.CompanyId ?? fallbackCompanyId,
            GuestId = reservationContext.GuestId,
            ReservationId = reservationContext.ReservationId,
            PropertyId = reservationContext.PropertyId
        };
    }

    private AIKnowledgeContext BuildKnowledgeContext(Property property, IReadOnlyCollection<QuestionContextCategory> categories, bool requiresAccessAuthorization)
    {
        var articles = property.PropertyKnowledgeArticles
            .Where(article => article.IsActive && article.CompanyId == property.CompanyId && article.PropertyId == property.Id)
            .Where(article => IsRelevant(article.Title, article.Content, categories))
            .Where(article => !IsSensitiveAccessContent(article.Title, article.Content))
            .Where(article => !requiresAccessAuthorization || !IsRelevant(article.Title, article.Content, [QuestionContextCategory.PropertyAccess]))
            .OrderBy(article => article.Title)
            .Take(options.Value.MaxKnowledgeArticles)
            .Select(article => new AIKnowledgeArticleContext { Title = article.Title, Content = article.Content })
            .ToList();

        var amenities = property.PropertyAmenities
            .Where(amenity => amenity.IsActive && IsRelevant(amenity.Name, amenity.Description, categories))
            .OrderBy(amenity => amenity.Name)
            .Select(amenity => new AIAmenityContext { Name = amenity.Name, Description = amenity.Description })
            .ToList();

        var houseRules = property.PropertyHouseRules
            .Where(rule => rule.IsActive && IsHouseRuleRelevant(rule, categories))
            .OrderBy(rule => rule.Title)
            .Take(options.Value.MaxHouseRules)
            .Select(rule => new AIHouseRuleContext { Title = rule.Title, Description = rule.Description })
            .ToList();

        var recommendations = property.PropertyRecommendations
            .Where(recommendation => recommendation.IsActive && IsRecommendationRelevant(recommendation, categories))
            .OrderBy(recommendation => recommendation.Name)
            .Take(options.Value.MaxRecommendations)
            .Select(recommendation => new AIRecommendationContext
            {
                Name = recommendation.Name,
                Category = recommendation.Category,
                Description = recommendation.Description,
                Address = recommendation.Address,
                PhoneNumber = recommendation.PhoneNumber
            })
            .ToList();

        var emergencyContacts = property.PropertyEmergencyContacts
            .Where(contact => contact.IsActive && categories.Contains(QuestionContextCategory.Emergency))
            .OrderBy(contact => contact.Role)
            .ThenBy(contact => contact.Name)
            .Take(options.Value.MaxEmergencyContacts)
            .Select(contact => new AIEmergencyContactContext { Name = contact.Name, Role = contact.Role, PhoneNumber = contact.PhoneNumber })
            .ToList();

        return new AIKnowledgeContext
        {
            Articles = articles,
            Amenities = amenities,
            HouseRules = houseRules,
            Recommendations = recommendations,
            EmergencyContacts = emergencyContacts
        };
    }

    private static bool IsRelevant(string primaryText, string? secondaryText, IReadOnlyCollection<QuestionContextCategory> categories)
    {
        if (categories.Count == 1 && categories.Contains(QuestionContextCategory.General))
        {
            return false;
        }

        var text = $"{primaryText} {secondaryText}".ToLowerInvariant();
        return categories.Any(category => CategoryKeywords(category).Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsHouseRuleRelevant(PropertyHouseRule rule, IReadOnlyCollection<QuestionContextCategory> categories)
    {
        return categories.Contains(QuestionContextCategory.HouseRules)
            || IsRelevant(rule.Title, rule.Description, categories);
    }

    private static bool IsRecommendationRelevant(PropertyRecommendation recommendation, IReadOnlyCollection<QuestionContextCategory> categories)
    {
        var category = recommendation.Category.ToLowerInvariant();
        return (categories.Contains(QuestionContextCategory.Restaurant) && ContainsAny(category, ["restaurant", "food", "cafe", "dining"]))
            || (categories.Contains(QuestionContextCategory.Transportation) && ContainsAny(category, ["transport", "driver", "taxi", "airport"]))
            || (categories.Contains(QuestionContextCategory.Laundry) && ContainsAny(category, ["laundry"]))
            || IsRelevant(recommendation.Name, $"{recommendation.Category} {recommendation.Description}", categories);
    }

    private static bool IsSensitiveAccessContent(string title, string? content)
    {
        var text = $"{title} {content}".ToLowerInvariant();
        return ContainsAny(text, ["door code", "lockbox", "smart lock", "gate code", "alarm code", "access code", "pin code", "password"]);
    }

    private static string DetermineStayPhase(Reservation reservation, DateTimeOffset currentTimestamp)
    {
        var currentDate = DateOnly.FromDateTime(currentTimestamp.UtcDateTime);
        if (currentDate < reservation.CheckInDate)
        {
            return "PreArrival";
        }

        if (currentDate > reservation.CheckOutDate)
        {
            return "PostStay";
        }

        return reservation.Status switch
        {
            ReservationStatus.ReadyForCheckIn => "ReadyForCheckIn",
            ReservationStatus.CheckedIn or ReservationStatus.ActiveStay or ReservationStatus.CheckOutPending => "ActiveStay",
            ReservationStatus.CheckedOut or ReservationStatus.PostStay or ReservationStatus.Completed => "PostStay",
            _ => "InStayWindow"
        };
    }

    private async Task AuditAsync(
        Guid? companyId,
        Guid? guestId,
        Guid? reservationId,
        string reservationContextOutcome,
        IReadOnlyCollection<QuestionContextCategory> categories,
        AIContextBuildResult result,
        int knowledgeArticleCount,
        int recommendationCount,
        CancellationToken cancellationToken)
    {
        await aiContextRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "AIContext",
            EntityId = reservationId ?? Guid.Empty,
            Action = "Built",
            Details = JsonSerializer.Serialize(new
            {
                currentTenantContext.CorrelationId,
                CompanyId = companyId,
                GuestId = guestId,
                ReservationContextOutcome = reservationContextOutcome,
                QuestionCategories = categories.Select(category => category.ToString()).ToList(),
                KnowledgeArticleCount = knowledgeArticleCount,
                RecommendationCount = recommendationCount,
                ContextBuildOutcome = result.Outcome.ToString(),
                result.EscalationReason
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await aiContextRepository.SaveChangesAsync(cancellationToken);
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

    private static bool ContainsAny(string value, IReadOnlyCollection<string> keywords)
    {
        return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyCollection<string> CategoryKeywords(QuestionContextCategory category)
    {
        return category switch
        {
            QuestionContextCategory.WiFi => ["wifi", "wi-fi", "internet", "network"],
            QuestionContextCategory.CheckIn => ["check in", "check-in", "arrival", "checkin"],
            QuestionContextCategory.CheckOut => ["check out", "check-out", "checkout", "departure"],
            QuestionContextCategory.Parking => ["parking", "park", "garage", "car"],
            QuestionContextCategory.HouseRules => ["rule", "quiet", "noise", "smoking", "party", "pet"],
            QuestionContextCategory.Amenities => ["amenity", "pool", "gym", "kitchen", "air conditioning", "ac"],
            QuestionContextCategory.Emergency => ["emergency", "hospital", "police", "fire", "ambulance", "doctor"],
            QuestionContextCategory.Restaurant => ["restaurant", "food", "cafe", "dining", "breakfast", "lunch", "dinner"],
            QuestionContextCategory.Transportation => ["transport", "taxi", "uber", "bolt", "driver", "airport", "transfer"],
            QuestionContextCategory.Laundry => ["laundry", "washer", "washing", "dryer", "iron"],
            QuestionContextCategory.PropertyAccess => ["key", "access", "door", "lockbox", "gate", "alarm", "unlock"],
            _ => []
        };
    }
}
