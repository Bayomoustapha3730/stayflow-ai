using System.Text;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class PropertyKnowledgeRanker : IPropertyKnowledgeRanker
{
    public PropertyKnowledgeRankingResult Rank(
        ConversationContext context,
        GuestIntentResult intent,
        string latestGuestMessage,
        int maxSelectedItems,
        int maxSelectedCharacters)
    {
        var candidates = context.ApprovedKnowledgeItems
            .Where(item => item.IsApproved)
            .Select(item => Score(item, intent, latestGuestMessage))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Item.Priority)
            .ThenByDescending(item => item.Item.LastUpdated ?? DateTimeOffset.MinValue)
            .ThenBy(item => item.Item.Title, StringComparer.Ordinal)
            .ToList();

        var selected = new List<ConversationContextKnowledgeItem>();
        var selectedCharacters = 0;

        foreach (var candidate in candidates)
        {
            if (selected.Count >= maxSelectedItems)
            {
                break;
            }

            if (selectedCharacters + candidate.Item.Content.Length > maxSelectedCharacters)
            {
                continue;
            }

            selected.Add(candidate.Item);
            selectedCharacters += candidate.Item.Content.Length;
        }

        var ambiguous = candidates.Count > 1
            && candidates[0].Score == candidates[1].Score
            && candidates[0].Item.Category != candidates[1].Item.Category;

        var reasons = new List<string>
        {
            $"Selected {selected.Count} approved knowledge item(s) within a {maxSelectedCharacters} character budget.",
            $"Rejected {Math.Max(0, candidates.Count - selected.Count)} candidate(s) because of ranking or character limits."
        };

        if (ambiguous)
        {
            reasons.Add("Top knowledge candidates had equal deterministic ranking scores.");
        }

        return new PropertyKnowledgeRankingResult(
            candidates,
            selected,
            Math.Max(0, candidates.Count - selected.Count),
            ambiguous,
            reasons);
    }

    private static PropertyKnowledgeCandidate Score(
        ConversationContextKnowledgeItem item,
        GuestIntentResult intent,
        string latestGuestMessage)
    {
        var score = 0;
        var reasons = new List<string>();
        var normalizedGuest = Normalize(latestGuestMessage);
        var normalizedTitle = Normalize(item.Title);
        var normalizedContent = Normalize(item.Content);
        var normalizedTags = item.Tags.Select(Normalize).Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList();

        if (CategoryMatchesIntent(item.Category, intent.Intent))
        {
            score += 40;
            reasons.Add("Category matched detected intent.");
        }

        var intentTerms = intent.MatchedTerms;
        foreach (var term in intentTerms)
        {
            if (normalizedTitle.Contains(term, StringComparison.Ordinal))
            {
                score += 20;
                reasons.Add($"Title matched '{term}'.");
            }

            if (normalizedContent.Contains(term, StringComparison.Ordinal))
            {
                score += 10;
                reasons.Add($"Content matched '{term}'.");
            }

            if (normalizedTags.Any(tag => tag.Contains(term, StringComparison.Ordinal)))
            {
                score += 12;
                reasons.Add($"Tag matched '{term}'.");
            }
        }

        foreach (var phrase in ExtractPhrases(normalizedGuest))
        {
            if (phrase.Length < 4)
            {
                continue;
            }

            if (normalizedTitle.Contains(phrase, StringComparison.Ordinal) || normalizedContent.Contains(phrase, StringComparison.Ordinal))
            {
                score += 6;
                reasons.Add($"Matched phrase overlap '{phrase}'.");
            }

            if (normalizedTags.Any(tag => tag.Contains(phrase, StringComparison.Ordinal)))
            {
                score += 8;
                reasons.Add($"Tag overlap matched '{phrase}'.");
            }
        }

        score += Math.Clamp(item.Priority, 0, 10) * 3;
        if (item.Priority > 0)
        {
            reasons.Add("Priority boost applied.");
        }

        if (item.LastUpdated.HasValue)
        {
            var ageDays = (DateTimeOffset.UtcNow - item.LastUpdated.Value).TotalDays;
            if (ageDays <= 30)
            {
                score += 6;
                reasons.Add("Recency bonus for recent update.");
            }
            else if (ageDays <= 90)
            {
                score += 3;
                reasons.Add("Small recency bonus.");
            }
        }

        return new PropertyKnowledgeCandidate(item, score, reasons);
    }

    private static bool CategoryMatchesIntent(PropertyKnowledgeCategory category, GuestIntent intent)
    {
        return (category, intent) switch
        {
            (PropertyKnowledgeCategory.WiFi, GuestIntent.WiFi) => true,
            (PropertyKnowledgeCategory.CheckIn, GuestIntent.CheckIn or GuestIntent.EarlyCheckIn or GuestIntent.LateArrival) => true,
            (PropertyKnowledgeCategory.Checkout, GuestIntent.Checkout) => true,
            (PropertyKnowledgeCategory.Parking, GuestIntent.Parking) => true,
            (PropertyKnowledgeCategory.HouseRules, GuestIntent.HouseRules or GuestIntent.Noise) => true,
            (PropertyKnowledgeCategory.Amenities, GuestIntent.Amenities) => true,
            (PropertyKnowledgeCategory.Laundry, GuestIntent.Laundry) => true,
            (PropertyKnowledgeCategory.Thermostat, GuestIntent.Thermostat) => true,
            (PropertyKnowledgeCategory.Trash, GuestIntent.Trash) => true,
            (PropertyKnowledgeCategory.Emergency, GuestIntent.Emergency or GuestIntent.Maintenance) => true,
            (PropertyKnowledgeCategory.Accessibility, GuestIntent.Accessibility) => true,
            (PropertyKnowledgeCategory.LocalRecommendations, GuestIntent.GeneralQuestion) => true,
            (PropertyKnowledgeCategory.Maintenance, GuestIntent.Maintenance) => true,
            _ => false
        };
    }

    private static IReadOnlyCollection<string> ExtractPhrases(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var phrases = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < words.Length; i++)
        {
            phrases.Add(words[i]);
            if (i + 1 < words.Length)
            {
                phrases.Add($"{words[i]} {words[i + 1]}");
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

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        return string.Join(' ', builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
