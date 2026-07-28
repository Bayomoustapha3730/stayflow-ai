using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class PropertyKnowledgeRanker(
    IOptions<KnowledgeRetrievalOptions> options,
    IKnowledgeSimilarityScorer similarityScorer) : IPropertyKnowledgeRanker
{
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "the", "is", "are", "am", "to", "for", "of", "in", "on", "at", "and",
        "or", "i", "you", "me", "my", "we", "our", "please", "can", "what", "where", "when",
        "how", "do", "does", "que", "quel", "quelles", "est", "le", "la", "les", "de", "des",
        "du", "un", "une", "et", "ou"
    ];

    public KnowledgeRetrievalResult Rank(
        ConversationContext context,
        GuestIntentResult intent,
        string latestGuestMessage,
        int maxSelectedItems,
        int maxSelectedCharacters)
    {
        var retrievalOptions = options.Value;
        var boundedMaxSelected = Math.Clamp(Math.Min(maxSelectedItems, retrievalOptions.MaxSelectedItems), 1, 8);
        var boundedMaxCharacters = Math.Max(500, Math.Min(maxSelectedCharacters, retrievalOptions.ContextCharacterBudget));
        var normalizedQuery = Normalize(latestGuestMessage);
        var queryTokens = Tokenize(latestGuestMessage)
            .Where(token => !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var preferredCategories = PreferredCategories(intent.Intent);
        var categoryRestricted = intent.Intent != GuestIntent.Unknown && preferredCategories.Count > 0;
        var emergencyIntent = IsEmergencyIntent(intent, latestGuestMessage);
        var topCandidateCount = Math.Clamp(retrievalOptions.TopCandidateCount, 1, 10);

        var candidates = context.ApprovedKnowledgeItems
            .Where(item => item.IsApproved)
            .Where(item => emergencyIntent
                ? item.Category == PropertyKnowledgeCategory.Emergency
                : item.Category != PropertyKnowledgeCategory.Emergency)
            .Select(item => Score(item, intent, normalizedQuery, queryTokens, preferredCategories, retrievalOptions))
            .Where(item => item.Score >= retrievalOptions.MinimumScore)
            .OrderByDescending(item => item.Score)
            .Take(Math.Clamp(retrievalOptions.MaxCandidates, 1, 100))
            .Take(topCandidateCount)
            .OrderByDescending(item => item.SemanticScore)
            .ThenByDescending(item => item.Score)
            .ThenByDescending(item => item.Item.Priority)
            .ThenBy(item => item.Item.Title, StringComparer.Ordinal)
            .Select((candidate, index) => candidate with { Rank = index + 1 })
            .ToList();

        var selected = new List<KnowledgeRetrievalCandidate>();
        var selectedCharacters = 0;
        var seenFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var wasTruncated = false;

        var highConfidenceCutoff = candidates.Count > 1
            ? candidates[1].Score + retrievalOptions.MinimumScoreGap
            : retrievalOptions.HighConfidenceScore;

        var limit = candidates.Count > 0 && candidates[0].Score >= highConfidenceCutoff
            ? Math.Min(2, boundedMaxSelected)
            : boundedMaxSelected;

        foreach (var candidate in candidates)
        {
            if (selected.Count >= limit)
            {
                break;
            }

            if (selectedCharacters + candidate.Item.Content.Length > boundedMaxCharacters)
            {
                wasTruncated = true;
                continue;
            }

            var fingerprint = Normalize(candidate.Item.Title) + "|" + Normalize(candidate.Item.Content);
            if (!seenFingerprints.Add(fingerprint))
            {
                continue;
            }

            selected.Add(candidate);
            selectedCharacters += candidate.Item.Content.Length;
        }

        var ambiguous = candidates.Count > 1
            && (candidates[0].Category != candidates[1].Category)
            && Math.Abs(candidates[0].Score - candidates[1].Score) < retrievalOptions.MinimumScoreGap;

        var confidence = ComputeConfidence(candidates, retrievalOptions, ambiguous);
        if (emergencyIntent && candidates.FirstOrDefault()?.Category == PropertyKnowledgeCategory.Emergency)
        {
            confidence = Math.Max(confidence, retrievalOptions.MediumConfidenceScore + 5);
        }
        var confidenceLevel = candidates.Count == 0
            ? KnowledgeConfidenceLevel.None
            : confidence >= retrievalOptions.HighConfidenceScore
            ? KnowledgeConfidenceLevel.High
            : confidence >= retrievalOptions.MediumConfidenceScore
                ? KnowledgeConfidenceLevel.Medium
                : KnowledgeConfidenceLevel.Low;

        var reasonCode = ResolveReasonCode(candidates, intent.Intent, confidenceLevel, ambiguous);
        var clarificationChoices = BuildClarificationChoices(candidates);
        var requiresClarification = confidenceLevel == KnowledgeConfidenceLevel.Low
            && ambiguous
            && !emergencyIntent;

        if (intent.Intent == GuestIntent.Unknown)
        {
            selected.Clear();
            confidenceLevel = KnowledgeConfidenceLevel.Low;
            reasonCode = KnowledgeRetrievalReasonCode.NoMatch;
            requiresClarification = false;
        }

        var confidenceRejected = candidates.Count > 0 && confidence < retrievalOptions.MinimumConfidenceScore && !emergencyIntent;
        if (confidenceRejected)
        {
            selected.Clear();
        }

        var escalationRecommended = confidenceLevel == KnowledgeConfidenceLevel.Low
            && selected.Count == 0
            && !emergencyIntent;

        var reasons = new List<string>
        {
            $"Selected {selected.Count} approved knowledge item(s) within a {boundedMaxCharacters} character budget.",
            $"Rejected {Math.Max(0, candidates.Count - selected.Count)} candidate(s) because of ranking, deduplication, or character limits."
        };

        if (ambiguous)
        {
            reasons.Add("Top knowledge candidates had equal deterministic ranking scores.");
        }

        if (confidenceRejected)
        {
            reasons.Add($"Top candidate confidence {confidence:0.0} was below minimum threshold {retrievalOptions.MinimumConfidenceScore:0.0}.");
        }

        return new KnowledgeRetrievalResult(
            intent,
            candidates,
            selected,
            confidence,
            confidenceLevel,
            reasonCode,
            categoryRestricted,
            wasTruncated,
            requiresClarification,
            escalationRecommended,
            clarificationChoices,
            reasons);
    }

    private KnowledgeRetrievalCandidate Score(
        ConversationContextKnowledgeItem item,
        GuestIntentResult intent,
        string normalizedQuery,
        IReadOnlyCollection<string> queryTokens,
        IReadOnlyCollection<PropertyKnowledgeCategory> preferredCategories,
        KnowledgeRetrievalOptions retrievalOptions)
    {
        var lexicalScore = 0d;
        var signals = new List<string>();
        var normalizedTitle = Normalize(item.Title);
        var normalizedSummary = Normalize(item.Summary ?? string.Empty);
        var normalizedContent = Normalize(item.Content);
        var normalizedTags = item.Tags.Select(Normalize).Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList();

        if (!string.IsNullOrWhiteSpace(normalizedQuery) && normalizedTitle == normalizedQuery)
        {
            lexicalScore += retrievalOptions.TitleMatchWeight * 2.2;
            signals.Add(nameof(KnowledgeRetrievalReasonCode.ExactTitleMatch));
        }

        if (preferredCategories.Contains(item.Category))
        {
            lexicalScore += retrievalOptions.CategoryMatchWeight;
            signals.Add(nameof(KnowledgeRetrievalReasonCode.CategoryAndKeywordMatch));
        }

        if (intent.Intent != GuestIntent.Unknown && preferredCategories.Count > 0 && !preferredCategories.Contains(item.Category))
        {
            lexicalScore -= retrievalOptions.UnrelatedCategoryPenalty;
        }

        if (intent.Intent == GuestIntent.PetPolicy)
        {
            var hasPetSignal = normalizedTitle.Contains("pet", StringComparison.Ordinal)
                || normalizedTitle.Contains("animal", StringComparison.Ordinal)
                || normalizedContent.Contains("pet", StringComparison.Ordinal)
                || normalizedContent.Contains("animal", StringComparison.Ordinal)
                || normalizedTags.Any(tag => tag.Contains("pet", StringComparison.Ordinal)
                    || tag.Contains("animal", StringComparison.Ordinal)
                    || tag.Contains("dog", StringComparison.Ordinal)
                    || tag.Contains("cat", StringComparison.Ordinal));

            if (!hasPetSignal)
            {
                lexicalScore -= retrievalOptions.CategoryMatchWeight * 1.5;
                signals.Add(nameof(KnowledgeRetrievalReasonCode.RestrictedByPolicy));
            }
        }

        if (intent.Intent != GuestIntent.Emergency && item.Category == PropertyKnowledgeCategory.Emergency)
        {
            lexicalScore -= retrievalOptions.EmergencyMismatchPenalty;
            signals.Add(nameof(KnowledgeRetrievalReasonCode.RestrictedByPolicy));
        }

        foreach (var term in intent.MatchedTerms.Select(Normalize).Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            if (normalizedTitle.Contains(term, StringComparison.Ordinal))
            {
                lexicalScore += retrievalOptions.TitleMatchWeight;
                signals.Add(nameof(KnowledgeRetrievalReasonCode.StrongKeywordMatch));
            }

            if (normalizedTags.Any(tag => tag.Contains(term, StringComparison.Ordinal)))
            {
                lexicalScore += retrievalOptions.TagMatchWeight;
                signals.Add(nameof(KnowledgeRetrievalReasonCode.TagMatch));
            }
        }

        var queryPhrases = BuildPhrases(queryTokens);
        foreach (var phrase in queryPhrases)
        {
            if (normalizedTitle.Contains(phrase, StringComparison.Ordinal))
            {
                lexicalScore += retrievalOptions.TitleMatchWeight * 1.4;
                signals.Add(nameof(KnowledgeRetrievalReasonCode.StrongKeywordMatch));
            }

            if (normalizedSummary.Contains(phrase, StringComparison.Ordinal))
            {
                lexicalScore += retrievalOptions.SummaryMatchWeight;
            }

            if (normalizedContent.Contains(phrase, StringComparison.Ordinal))
            {
                lexicalScore += retrievalOptions.ContentMatchWeight;
            }
        }

        var tokenOverlap = queryTokens.Count(token =>
            normalizedTitle.Contains(token, StringComparison.Ordinal)
            || normalizedSummary.Contains(token, StringComparison.Ordinal)
            || normalizedContent.Contains(token, StringComparison.Ordinal));

        lexicalScore += tokenOverlap * (retrievalOptions.ContentMatchWeight / 3.0);

        var semanticScore = similarityScorer.Score(item, intent, normalizedQuery, queryTokens);
        if (semanticScore > 0)
        {
            signals.Add(nameof(KnowledgeRetrievalReasonCode.SemanticMatch));
        }

        var priorityScore = Math.Clamp(item.Priority, 0, 10) * retrievalOptions.PriorityWeight;
        if (item.Priority > 0)
        {
            signals.Add("PriorityTieBreaker");
        }

        var recencyScore = 0d;
        if (item.LastUpdated.HasValue)
        {
            var ageDays = (DateTimeOffset.UtcNow - item.LastUpdated.Value).TotalDays;
            recencyScore = ageDays <= 30 ? 1.5 : ageDays <= 90 ? 0.8 : 0;
        }

        var score = (semanticScore * 100.0)
            + (lexicalScore * 0.75)
            + (priorityScore * 0.2)
            + recencyScore;

        return new KnowledgeRetrievalCandidate(
            item.SourceId,
            item.Category,
            score,
            semanticScore,
            signals.Distinct(StringComparer.Ordinal).ToArray(),
            0,
            item);
    }

    private static IReadOnlyCollection<PropertyKnowledgeCategory> PreferredCategories(GuestIntent intent)
    {
        return intent switch
        {
            GuestIntent.WiFi => [PropertyKnowledgeCategory.WiFi],
            GuestIntent.CheckIn or GuestIntent.EarlyCheckIn or GuestIntent.LateArrival => [PropertyKnowledgeCategory.CheckIn, PropertyKnowledgeCategory.Accessibility],
            GuestIntent.Checkout => [PropertyKnowledgeCategory.Checkout],
            GuestIntent.Parking => [PropertyKnowledgeCategory.Parking],
            GuestIntent.HouseRules or GuestIntent.Noise or GuestIntent.PetPolicy => [PropertyKnowledgeCategory.HouseRules],
            GuestIntent.LocalRecommendations => [PropertyKnowledgeCategory.LocalRecommendations],
            GuestIntent.Emergency => [PropertyKnowledgeCategory.Emergency],
            GuestIntent.Amenities => [PropertyKnowledgeCategory.Amenities, PropertyKnowledgeCategory.Laundry],
            GuestIntent.Access or GuestIntent.PropertyAccess => [PropertyKnowledgeCategory.CheckIn, PropertyKnowledgeCategory.WiFi, PropertyKnowledgeCategory.Parking],
            GuestIntent.Reservation => [PropertyKnowledgeCategory.CheckIn, PropertyKnowledgeCategory.Checkout],
            GuestIntent.Payment => [PropertyKnowledgeCategory.FAQ, PropertyKnowledgeCategory.Other],
            GuestIntent.HostContact => [PropertyKnowledgeCategory.FAQ, PropertyKnowledgeCategory.Other],
            GuestIntent.GeneralProperty => [PropertyKnowledgeCategory.FAQ, PropertyKnowledgeCategory.Other],
            GuestIntent.Maintenance => [PropertyKnowledgeCategory.Maintenance],
            _ => []
        };
    }

    private static double ComputeConfidence(
        IReadOnlyCollection<KnowledgeRetrievalCandidate> candidates,
        KnowledgeRetrievalOptions retrievalOptions,
        bool ambiguous)
    {
        var ordered = candidates.ToList();
        if (ordered.Count == 0)
        {
            return 0;
        }

        var topSemantic = ordered[0].SemanticScore * 100.0;
        var secondSemantic = ordered.Count > 1 ? ordered[1].SemanticScore * 100.0 : 0;
        var gap = topSemantic - secondSemantic;

        var confidence = Math.Clamp(topSemantic, 0, 100);
        if (gap >= retrievalOptions.MinimumScoreGap)
        {
            confidence = Math.Min(100, confidence + 8);
        }

        if (ambiguous)
        {
            confidence = Math.Max(0, confidence - 15);
        }

        return confidence;
    }

    private static bool IsEmergencyIntent(GuestIntentResult intent, string latestGuestMessage)
    {
        if (intent.Intent != GuestIntent.Emergency)
        {
            return false;
        }

        var normalized = Normalize(latestGuestMessage);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var emergencyTerms = new[]
        {
            "emergency", "urgent", "emergency help", "911", "fire", "smoke", "gas leak", "smell gas", "medical", "injured", "injury",
            "unsafe", "danger", "police", "ambulance", "urgence", "incendie", "fuite de gaz"
        };

        return emergencyTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal));
    }

    private static KnowledgeRetrievalReasonCode ResolveReasonCode(
        IReadOnlyCollection<KnowledgeRetrievalCandidate> candidates,
        GuestIntent intent,
        KnowledgeConfidenceLevel confidenceLevel,
        bool ambiguous)
    {
        if (intent == GuestIntent.Emergency)
        {
            return KnowledgeRetrievalReasonCode.EmergencyIntent;
        }

        if (candidates.Count == 0)
        {
            return KnowledgeRetrievalReasonCode.NoMatch;
        }

        if (ambiguous)
        {
            return KnowledgeRetrievalReasonCode.Ambiguous;
        }

        var top = candidates.First();
        if (top.MatchSignals.Contains(nameof(KnowledgeRetrievalReasonCode.ExactTitleMatch), StringComparer.Ordinal))
        {
            return KnowledgeRetrievalReasonCode.ExactTitleMatch;
        }

        if (top.MatchSignals.Contains(nameof(KnowledgeRetrievalReasonCode.CategoryAndKeywordMatch), StringComparer.Ordinal))
        {
            return KnowledgeRetrievalReasonCode.CategoryAndKeywordMatch;
        }

        if (top.MatchSignals.Contains(nameof(KnowledgeRetrievalReasonCode.TagMatch), StringComparer.Ordinal))
        {
            return KnowledgeRetrievalReasonCode.TagMatch;
        }

        return confidenceLevel == KnowledgeConfidenceLevel.Low
            ? KnowledgeRetrievalReasonCode.WeakMatch
            : KnowledgeRetrievalReasonCode.StrongKeywordMatch;
    }

    private static IReadOnlyCollection<string> BuildClarificationChoices(IReadOnlyCollection<KnowledgeRetrievalCandidate> candidates)
    {
        return candidates
            .Take(3)
            .Select(candidate => candidate.Category switch
            {
                PropertyKnowledgeCategory.CheckIn => "property entry",
                PropertyKnowledgeCategory.WiFi => "Wi-Fi access",
                PropertyKnowledgeCategory.Parking => "parking",
                PropertyKnowledgeCategory.Checkout => "check-out details",
                _ => candidate.Category.ToString()
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyCollection<string> BuildPhrases(IReadOnlyCollection<string> tokens)
    {
        var phrases = new HashSet<string>(StringComparer.Ordinal);
        var tokenList = tokens.ToList();
        for (var i = 0; i < tokenList.Count; i++)
        {
            phrases.Add(tokenList[i]);
            if (i + 1 < tokenList.Count)
            {
                phrases.Add($"{tokenList[i]} {tokenList[i + 1]}");
            }
        }

        return phrases;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var deaccented = value
            .Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            .Select(char.ToLowerInvariant)
            .ToArray();

        var builder = new StringBuilder(deaccented.Length);
        foreach (var ch in deaccented)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static List<string> Tokenize(string value)
    {
        return Normalize(value)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.EndsWith('s') && token.Length > 3 ? token[..^1] : token)
            .ToList();
    }
}
