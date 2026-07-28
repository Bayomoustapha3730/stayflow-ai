using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class PropertyKnowledgeRetriever(
    IPropertyKnowledgeRanker ranker,
    IKnowledgeReranker reranker,
    IKnowledgeQueryExpander queryExpander,
    IKnowledgeSemanticSimilarityService semanticSimilarityService,
    IKnowledgeSimilarityScorer legacySimilarityScorer,
    Microsoft.Extensions.Options.IOptions<StayFlow.Api.Services.AI.Orchestration.ConciergeIntelligenceOptions> options) : IPropertyKnowledgeRetriever
{
    public KnowledgeRetrievalResult Retrieve(ConversationContext context, KnowledgeRetrievalRequest request)
    {
        var intelligence = options.Value;
        var maxCandidates = Math.Clamp(request.MaximumCandidates, 1, intelligence.MaximumCandidates);
        var maxSelected = Math.Clamp(request.MaximumSelectedItems, 1, intelligence.MaximumSelectedItems);
        var maxChars = Math.Max(1000, Math.Min(request.ContextCharacterBudget, intelligence.ContextCharacterBudget));

        var expansion = queryExpander.Expand(request.Query, request.IntentResult);
        var intents = request.IntentResult.AllIntents().Take(Math.Clamp(intelligence.MaximumIntents, 1, 3)).ToList();

        var aggregateCandidates = new Dictionary<string, KnowledgeRetrievalCandidate>(StringComparer.Ordinal);
        var reasons = new List<string>();

        foreach (var intent in intents)
        {
            var guestIntent = new GuestIntentResult(
                intent,
                request.IntentResult.Confidence,
                request.IntentResult.MatchedSignals,
                request.IntentResult.IsAmbiguous,
                "v2-intent");

            var result = ranker.Rank(context, guestIntent, expansion.NormalizedQuery, maxSelected, maxChars);
            reasons.AddRange(result.Reasons);

            foreach (var candidate in result.Candidates)
            {
                var semantic = semanticSimilarityService.Score(candidate.Item, request.IntentResult, expansion);
                var legacySemantic = legacySimilarityScorer.Score(candidate.Item, guestIntent, expansion.NormalizedQuery, expansion.ExpandedTerms.ToArray());
                var normalizedPriority = Math.Clamp(candidate.Item.Priority, 0, 10) / 10d;
                var intentScore = intent == request.IntentResult.PrimaryIntent ? 1.0 : 0.75;
                var lexicalScore = Math.Clamp(candidate.Score / 100d, 0, 1);
                var semanticScore = Math.Clamp((semantic + legacySemantic) / 2.0, 0, 1);

                var final = (intelligence.IntentWeight * intentScore)
                    + (intelligence.LexicalWeight * lexicalScore)
                    + (intelligence.SemanticWeight * semanticScore)
                    + (intelligence.PriorityWeight * normalizedPriority);

                if (request.IntentResult.PrimaryIntent != GuestIntent.Emergency
                    && candidate.Category == StayFlow.Api.Models.PropertyKnowledgeCategory.Emergency)
                {
                    final -= intelligence.EmergencyMismatchPenalty;
                }

                var updated = candidate with
                {
                    Score = final,
                    LexicalScore = lexicalScore,
                    SemanticScore = semanticScore,
                    IntentScore = intentScore,
                    PriorityScore = normalizedPriority,
                    FinalScore = final,
                    MatchSignals = candidate.MatchSignals.Concat(expansion.MatchedPhrases).Distinct(StringComparer.Ordinal).ToArray()
                };

                if (!aggregateCandidates.TryGetValue(candidate.ArticleId, out var existing) || updated.Score > existing.Score)
                {
                    aggregateCandidates[candidate.ArticleId] = updated;
                }
            }
        }

        var ordered = aggregateCandidates.Values
            .Where(candidate => candidate.FinalScore >= intelligence.MinimumFinalScore)
            .OrderByDescending(candidate => candidate.FinalScore)
            .ThenByDescending(candidate => candidate.SemanticScore)
            .ThenByDescending(candidate => candidate.LexicalScore)
            .ThenBy(candidate => candidate.Item.Title, StringComparer.Ordinal)
            .Take(maxCandidates)
            .Select((candidate, idx) => candidate with { Rank = idx + 1 })
            .ToList();

        if (ordered.Count == 0)
        {
            var fallback = BuildLowConfidenceFallback(context, request.IntentResult.PrimaryIntent);
            if (fallback is not null)
            {
                ordered = [fallback];
            }
        }

        var reranked = reranker.Rerank(ordered, context, request, maxCandidates).ToList();
        var selected = SelectCoverageAware(reranked, maxSelected, maxChars, intents);
        selected = FilterSelectedToCurrentIntents(selected, intents);
        var confidence = ComputeConfidence(selected, reranked, request.IntentResult, intelligence, out var reasonCode, out var needsClarification);

        return new KnowledgeRetrievalResult(
            request.IntentResult.ToGuestIntentResult(),
            reranked,
            selected,
            confidence.Score,
            confidence.Level,
            reasonCode,
            request.IntentResult.PrimaryIntent != GuestIntent.Unknown,
            selected.Sum(item => item.Item.Content.Length) >= maxChars,
            needsClarification,
            confidence.Level is KnowledgeConfidenceLevel.None or KnowledgeConfidenceLevel.Low,
            BuildClarificationChoices(intents),
            reasons.Distinct(StringComparer.Ordinal).ToArray())
        {
            IsAmbiguous = needsClarification,
            ClarificationPrompt = needsClarification
                ? $"Are you asking about {string.Join(", ", BuildClarificationChoices(intents).Take(3))}?"
                : null,
            IntentResult = request.IntentResult,
            EvaluationMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["IntentCount"] = intents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["CandidateCount"] = reranked.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["SelectedCount"] = selected.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["RejectedOffIntentCount"] = (Math.Max(0, reranked.Count - selected.Count)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };
    }

    private static List<KnowledgeRetrievalCandidate> FilterSelectedToCurrentIntents(
        IReadOnlyCollection<KnowledgeRetrievalCandidate> selected,
        IReadOnlyCollection<GuestIntent> intents)
    {
        var constrainedIntents = intents.Where(IsCategoryConstrainedIntent).ToList();
        if (constrainedIntents.Count == 0)
        {
            return selected.ToList();
        }

        var intentSet = intents.ToHashSet();
        return selected
            .Where(candidate => intentSet.Any(intent => !IsCategoryConstrainedIntent(intent) || IntentMatchesCategory(intent, candidate.Category)))
            .ToList();
    }

    private static bool IsCategoryConstrainedIntent(GuestIntent intent)
    {
        return intent is GuestIntent.WiFi
            or GuestIntent.CheckIn
            or GuestIntent.PropertyAccess
            or GuestIntent.Checkout
            or GuestIntent.Parking
            or GuestIntent.HouseRules
            or GuestIntent.PetPolicy
            or GuestIntent.Emergency
            or GuestIntent.LocalRecommendations
            or GuestIntent.Amenities;
    }

    private static List<KnowledgeRetrievalCandidate> SelectCoverageAware(
        IReadOnlyCollection<KnowledgeRetrievalCandidate> ordered,
        int maxSelected,
        int maxChars,
        IReadOnlyCollection<GuestIntent> intents)
    {
        var selected = new List<KnowledgeRetrievalCandidate>();
        var selectedIds = new HashSet<string>(StringComparer.Ordinal);
        var currentChars = 0;

        foreach (var intent in intents)
        {
            var candidate = ordered.FirstOrDefault(item => !selectedIds.Contains(item.ArticleId) && IntentMatchesCategory(intent, item.Category));
            if (candidate is null)
            {
                continue;
            }

            if (currentChars + candidate.Item.Content.Length > maxChars)
            {
                continue;
            }

            selected.Add(candidate);
            selectedIds.Add(candidate.ArticleId);
            currentChars += candidate.Item.Content.Length;
            if (selected.Count >= maxSelected)
            {
                return selected;
            }
        }

        foreach (var candidate in ordered)
        {
            if (selected.Count >= maxSelected)
            {
                break;
            }

            if (!selectedIds.Add(candidate.ArticleId))
            {
                continue;
            }

            if (currentChars + candidate.Item.Content.Length > maxChars)
            {
                continue;
            }

            selected.Add(candidate);
            currentChars += candidate.Item.Content.Length;
        }

        return selected;
    }

    private static bool IntentMatchesCategory(GuestIntent intent, StayFlow.Api.Models.PropertyKnowledgeCategory category)
    {
        return intent switch
        {
            GuestIntent.WiFi => category == StayFlow.Api.Models.PropertyKnowledgeCategory.WiFi,
            GuestIntent.CheckIn or GuestIntent.PropertyAccess => category is StayFlow.Api.Models.PropertyKnowledgeCategory.CheckIn or StayFlow.Api.Models.PropertyKnowledgeCategory.Accessibility,
            GuestIntent.Checkout => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Checkout,
            GuestIntent.Parking => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Parking,
            GuestIntent.HouseRules or GuestIntent.PetPolicy => category == StayFlow.Api.Models.PropertyKnowledgeCategory.HouseRules,
            GuestIntent.Emergency => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Emergency,
            GuestIntent.LocalRecommendations => category == StayFlow.Api.Models.PropertyKnowledgeCategory.LocalRecommendations,
            GuestIntent.Amenities => category == StayFlow.Api.Models.PropertyKnowledgeCategory.Amenities,
            _ => false
        };
    }

    private static KnowledgeConfidenceResult ComputeConfidence(
        IReadOnlyCollection<KnowledgeRetrievalCandidate> selected,
        IReadOnlyCollection<KnowledgeRetrievalCandidate> ordered,
        ConversationIntentResult intentResult,
        StayFlow.Api.Services.AI.Orchestration.ConciergeIntelligenceOptions intelligence,
        out KnowledgeRetrievalReasonCode reasonCode,
        out bool needsClarification)
    {
        var top = ordered.FirstOrDefault();
        var second = ordered.Skip(1).FirstOrDefault();

        if (top is null)
        {
            reasonCode = KnowledgeRetrievalReasonCode.MissingKnowledgeForIntent;
            needsClarification = false;
            return new KnowledgeConfidenceResult(0, KnowledgeConfidenceLevel.None, 0, 0, 0, intentResult.Confidence, 0, false, KnowledgeRetrievalReasonCode.MissingKnowledgeForIntent);
        }

        var gap = second is null ? top.FinalScore : top.FinalScore - second.FinalScore;
        var coverage = Math.Clamp((double)selected.Count / Math.Max(1, intentResult.AllIntents().Count), 0, 1);
        var score = Math.Clamp((top.FinalScore * 0.55) + (intentResult.Confidence * 0.30) + (coverage * 0.15), 0, 1);
        var explicitMultiIntent = intentResult.AllIntents().Count > 1
            && intentResult.MatchedSignals.Any(signal => !signal.StartsWith("context:", StringComparison.Ordinal));
        needsClarification = !explicitMultiIntent && (gap < intelligence.MinimumScoreGap || intentResult.IsAmbiguous);

        var level = score >= intelligence.HighConfidenceThreshold
            ? KnowledgeConfidenceLevel.High
            : score >= intelligence.MediumConfidenceThreshold
                ? KnowledgeConfidenceLevel.Medium
                : score > 0
                    ? KnowledgeConfidenceLevel.Low
                    : KnowledgeConfidenceLevel.None;

        reasonCode = level switch
        {
            KnowledgeConfidenceLevel.High when intentResult.PrimaryIntent == GuestIntent.Emergency => KnowledgeRetrievalReasonCode.EmergencyIntent,
            KnowledgeConfidenceLevel.High => KnowledgeRetrievalReasonCode.ExactIntentAndCategoryMatch,
            KnowledgeConfidenceLevel.Medium => KnowledgeRetrievalReasonCode.StrongIntentMatch,
            KnowledgeConfidenceLevel.Low when selected.Count == 0 => KnowledgeRetrievalReasonCode.MissingKnowledgeForIntent,
            KnowledgeConfidenceLevel.Low when needsClarification => KnowledgeRetrievalReasonCode.AmbiguousTopCandidates,
            KnowledgeConfidenceLevel.None => KnowledgeRetrievalReasonCode.UnsupportedQuestion,
            _ => KnowledgeRetrievalReasonCode.WeakMatch
        };

        return new KnowledgeConfidenceResult(score, level, top.FinalScore, second?.FinalScore ?? 0, gap, intentResult.Confidence, coverage, needsClarification, reasonCode);
    }

    private static IReadOnlyCollection<string> BuildClarificationChoices(IReadOnlyCollection<GuestIntent> intents)
    {
        return intents.Select(intent => intent switch
        {
            GuestIntent.CheckIn => "check-in time",
            GuestIntent.Checkout => "checkout",
            GuestIntent.WiFi => "Wi-Fi",
            GuestIntent.PropertyAccess or GuestIntent.Access => "property entry",
            GuestIntent.Parking => "parking",
            GuestIntent.PetPolicy => "pet policy",
            GuestIntent.HouseRules => "house rules",
            GuestIntent.LocalRecommendations => "local recommendations",
            GuestIntent.Amenities => "amenities",
            GuestIntent.Reservation => "reservation details",
            GuestIntent.Payment => "payment details",
            GuestIntent.HostContact => "host contact",
            GuestIntent.GeneralProperty or GuestIntent.GeneralQuestion => "your request",
            _ => "your request"
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static KnowledgeRetrievalCandidate? BuildLowConfidenceFallback(ConversationContext context, GuestIntent primaryIntent)
    {
        if (primaryIntent == GuestIntent.Unknown)
        {
            return null;
        }

        if (primaryIntent == GuestIntent.PetPolicy)
        {
            var petFallback = context.ApprovedKnowledgeItems
                .FirstOrDefault(item => item.IsApproved && item.Category == StayFlow.Api.Models.PropertyKnowledgeCategory.HouseRules);
            if (petFallback is null)
            {
                return null;
            }

            return new KnowledgeRetrievalCandidate(
                petFallback.SourceId,
                StayFlow.Api.Models.PropertyKnowledgeCategory.HouseRules,
                0.05,
                0,
                ["LowConfidenceFallback"],
                1,
                petFallback)
            {
                LexicalScore = 0,
                SemanticScore = 0,
                IntentScore = 0.2,
                PriorityScore = 0,
                FinalScore = 0.05
            };
        }

        if (primaryIntent is not (GuestIntent.GeneralProperty or GuestIntent.Payment or GuestIntent.HostContact))
        {
            return null;
        }

        var genericFallback = context.ApprovedKnowledgeItems
            .FirstOrDefault(item => item.IsApproved && item.Category != StayFlow.Api.Models.PropertyKnowledgeCategory.Emergency);
        if (genericFallback is null)
        {
            return null;
        }

        return new KnowledgeRetrievalCandidate(
            genericFallback.SourceId,
            StayFlow.Api.Models.PropertyKnowledgeCategory.Other,
            0.05,
            0,
            ["LowConfidenceFallback"],
            1,
            genericFallback)
        {
            LexicalScore = 0,
            SemanticScore = 0,
            IntentScore = 0.2,
            PriorityScore = 0,
            FinalScore = 0.05
        };
    }
}
