using System.Globalization;
using System.Text;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class KnowledgeQueryExpander : IKnowledgeQueryExpander
{
    private static readonly IReadOnlyDictionary<GuestIntent, string[]> Synonyms = new Dictionary<GuestIntent, string[]>
    {
        [GuestIntent.WiFi] = ["wifi", "wireless", "internet", "network", "ssid", "password"],
        [GuestIntent.CheckIn] = ["checkin", "arrival", "entry", "access", "get inside", "arrival time"],
        [GuestIntent.Checkout] = ["checkout", "departure", "leave", "vacate"],
        [GuestIntent.Parking] = ["parking", "garage", "driveway", "vehicle"],
        [GuestIntent.HouseRules] = ["house rules", "quiet hours", "smoking", "parties", "visitors"],
        [GuestIntent.PetPolicy] = ["pets", "dog", "cat", "animal", "service animal"],
        [GuestIntent.PropertyAccess] = ["entry", "door", "keypad", "access code", "unlock"],
        [GuestIntent.Emergency] = ["emergency", "fire", "smoke", "gas leak", "ambulance", "police"],
        [GuestIntent.LocalRecommendations] = ["restaurant", "nearby", "grocery", "attractions"],
    };

    private static readonly string[] EmergencyTerms = ["emergency", "fire", "smoke", "gas", "ambulance", "police", "danger"];

    public KnowledgeQueryExpansionResult Expand(string query, ConversationIntentResult intentResult)
    {
        var normalized = Normalize(query);
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var matchedPhrases = intentResult.MatchedSignals
            .Where(signal => !signal.StartsWith("fuzzy:", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var synonymSet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var intent in intentResult.AllIntents())
        {
            if (Synonyms.TryGetValue(intent, out var values))
            {
                foreach (var value in values)
                {
                    synonymSet.Add(Normalize(value));
                }
            }
        }

        var excludedEmergency = new List<string>();
        if (intentResult.PrimaryIntent != GuestIntent.Emergency)
        {
            foreach (var term in EmergencyTerms)
            {
                if (synonymSet.Remove(term))
                {
                    excludedEmergency.Add(term);
                }
            }
        }

        var expanded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            expanded.Add(token);
        }

        foreach (var phrase in matchedPhrases)
        {
            expanded.Add(Normalize(phrase));
        }

        foreach (var synonym in synonymSet)
        {
            if (!string.IsNullOrWhiteSpace(synonym))
            {
                expanded.Add(synonym);
            }
        }

        return new KnowledgeQueryExpansionResult(
            normalized,
            tokens,
            matchedPhrases,
            synonymSet.ToArray(),
            expanded.Take(40).ToArray(),
            excludedEmergency);
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var chars = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lowered = ch switch
            {
                '-' or '\'' or '\u2019' => ' ',
                _ => char.ToLowerInvariant(ch)
            };

            chars.Append(char.IsLetterOrDigit(lowered) ? lowered : ' ');
        }

        var normalized = string.Join(' ', chars
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        normalized = normalized.Replace("wi fi", "wifi", StringComparison.Ordinal)
            .Replace("check in", "checkin", StringComparison.Ordinal)
            .Replace("check out", "checkout", StringComparison.Ordinal);

        return normalized;
    }
}
