using System.Globalization;
using System.Text;

namespace StayFlow.Api.Services.AI.Intent;

public sealed class ConversationIntentRecognizer : IConversationIntentRecognizer
{
    private static readonly string[] AmbiguousAccessPhrases =
    [
        "access",
        "property access",
        "access work",
        "access information",
        "access instructions",
        "access details",
        "building access",
        "entry information",
        "entering",
        "getting in",
        "get in",
        "getting inside",
        "how does access work"
    ];

    private static readonly string[] CredentialRequestPhrases =
    [
        "door code",
        "keypad code",
        "entry code",
        "pin",
        "unlock code",
        "smart lock code",
        "lockbox code",
        "gate code",
        "digital key",
        "access code"
    ];

    private static readonly IReadOnlyDictionary<GuestIntent, string[]> IntentPhrases =
        new Dictionary<GuestIntent, string[]>
        {
            [GuestIntent.WiFi] =
            [
                "wifi", "wi fi", "wi-fi", "wireless", "internet", "network", "online", "router", "ssid",
                "password", "connect to internet", "wireless password", "mot de passe wi fi", "mot de passe wifi"
            ],
            [GuestIntent.CheckIn] =
            [
                "check in", "check-in", "arrival", "arrive", "arrival time", "get into the apartment", "get inside",
                "allowed inside", "enter the property", "early arrival", "late arrival", "arrivee", "arrivée"
            ],
            [GuestIntent.Checkout] =
            [
                "check out", "check-out", "checkout", "departure", "leaving time", "when do i need to leave",
                "vacate", "departure time", "depart", "départ", "partir"
            ],
            [GuestIntent.Parking] =
            [
                "parking", "park", "garage", "driveway", "vehicle", "car", "parking space", "stationnement", "stationner"
            ],
            [GuestIntent.HouseRules] =
            [
                "house rules", "quiet hours", "smoking", "smoke", "parties", "party", "visitors", "noise", "reglement", "règlement"
            ],
            [GuestIntent.PetPolicy] =
            [
                "pet", "pets", "dog", "dogs", "cat", "cats", "animal", "service animal", "emotional support animal",
                "bring my pet", "animaux"
            ],
            [GuestIntent.Emergency] =
            [
                "emergency", "urgent", "emergency help", "911", "fire", "there is smoke", "smoke in", "gas leak", "smell gas", "injured", "injury", "ambulance", "police", "unsafe", "danger",
                "break in", "break-in", "flooding", "medical emergency", "urgence", "incendie", "fuite de gaz"
            ],
            [GuestIntent.LocalRecommendations] =
            [
                "restaurant", "food nearby", "grocery", "attractions", "things to do", "nearby", "local recommendations",
                "restaurant a proximite", "restaurant à proximité"
            ],
            [GuestIntent.Amenities] = ["amenities", "facility", "pool", "gym", "kitchen"],
            [GuestIntent.PropertyAccess] =
            [
                "enter", "entry", "access", "get in", "get inside", "unlock", "door code", "key", "keypad", "access code", "front door", "acces", "accès", "entrer"
            ],
            [GuestIntent.Access] =
            [
                "tell me about access",
                "access information",
                "access instructions",
                "access details",
                "property access",
                "building access",
                "how does access work",
                "access work",
                "entry code"
            ],
            [GuestIntent.Reservation] = ["reservation", "booking", "stay dates", "confirmation"],
            [GuestIntent.Payment] = ["payment", "pay", "charge", "invoice", "receipt"],
            [GuestIntent.HostContact] = ["host", "contact host", "call host", "message host"],
            [GuestIntent.GeneralQuestion] = ["can you help me", "i have a question", "general question", "need help with"],
            [GuestIntent.GeneralProperty] = ["stay information", "question about stay", "property information"]
        };

    private static readonly IReadOnlyDictionary<GuestIntent, string[]> HighSignalTokens =
        new Dictionary<GuestIntent, string[]>
        {
            [GuestIntent.WiFi] = ["wifi", "wireless", "internet", "router", "ssid"],
            [GuestIntent.CheckIn] = ["checkin", "arrival", "arrive"],
            [GuestIntent.Checkout] = ["checkout", "departure", "depart", "leave"],
            [GuestIntent.Parking] = ["parking", "park", "garage"],
            [GuestIntent.HouseRules] = ["smoking", "smoke", "party", "quiet"],
            [GuestIntent.Emergency] = ["emergency", "urgent", "911", "fire", "ambulance", "danger", "unsafe"]
        };

    private static readonly HashSet<string> EmergencyTerms =
    [
        "emergency", "urgent", "emergency help", "911", "fire", "there is smoke", "smoke in", "gas leak", "smell gas", "injured", "injury", "ambulance", "police", "unsafe", "danger", "urgence", "incendie", "fuite de gaz"
    ];

    public ConversationIntentResult Recognize(
        string query,
        IReadOnlyCollection<string>? contextualHints = null,
        int maximumIntents = 3)
    {
        var normalized = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new ConversationIntentResult(
                GuestIntent.Unknown,
                [],
                0,
                ConversationIntentConfidenceLevel.Low,
                [],
                true,
                ["Could you tell me whether you need help with Wi-Fi, check-in, checkout, or parking?"],
                string.Empty);
        }

        var emergencyMentioned = EmergencyTerms.Any(term => ContainsPhrase(normalized, term));
        var hasCredentialRequest = ContainsAnyPhrase(normalized, CredentialRequestPhrases);
        var hasAmbiguousAccess = ContainsAnyPhrase(normalized, AmbiguousAccessPhrases);
        var directPropertyEntryRequest = IsDirectPropertyEntryRequest(normalized);
        var hasConnectQuestion = ContainsPhrase(normalized, "how do i connect")
            || ContainsPhrase(normalized, "how can i connect")
            || normalized == "connect"
            || normalized == "how connect";

        if (hasConnectQuestion)
        {
            var inferredFromContext = contextualHints is { Count: > 0 }
                ? contextualHints.Select(InferIntentFromHint).FirstOrDefault(intent => intent is not null)
                : null;

            if (inferredFromContext == GuestIntent.WiFi)
            {
                return new ConversationIntentResult(
                    GuestIntent.WiFi,
                    [],
                    0.78,
                    ConversationIntentConfidenceLevel.Medium,
                    ["context:wifi", "connect"],
                    false,
                    [],
                    normalized);
            }

            return new ConversationIntentResult(
                GuestIntent.WiFi,
                [],
                0.56,
                ConversationIntentConfidenceLevel.Medium,
                ["connect"],
                true,
                ["how to connect to the Wi-Fi"],
                normalized);
        }

        // Generic access language should prompt clarification unless it is an explicit credential request.
        if (!emergencyMentioned && hasAmbiguousAccess && !hasCredentialRequest && !directPropertyEntryRequest)
        {
            var intents = new[] { GuestIntent.Access, GuestIntent.CheckIn, GuestIntent.PropertyAccess, GuestIntent.WiFi };
            return new ConversationIntentResult(
            GuestIntent.Access,
            intents.Skip(1).ToArray(),
            0.62,
            ConversationIntentConfidenceLevel.Medium,
            ["ambiguous:access"],
            true,
            BuildClarificationOptions(intents),
            normalized);
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToArray();
        var scoreByIntent = new Dictionary<GuestIntent, double>();
        var signalsByIntent = new Dictionary<GuestIntent, HashSet<string>>();
        var firstPosByIntent = new Dictionary<GuestIntent, int>();

        foreach (var pair in IntentPhrases)
        {
            var score = 0d;
            var signals = new HashSet<string>(StringComparer.Ordinal);
            var firstPos = int.MaxValue;

            if (pair.Key == GuestIntent.Reservation
                && (normalized.Contains("extend", StringComparison.Ordinal)
                    || normalized.Contains("change", StringComparison.Ordinal)
                    || normalized.Contains("cancel", StringComparison.Ordinal)))
            {
                continue;
            }

            foreach (var phrase in pair.Value
                .Select(Normalize)
                .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
                .Distinct(StringComparer.Ordinal)
                .OrderByDescending(phrase => phrase.Length))
            {
                var idx = IndexOfPhrase(normalized, phrase);
                if (idx >= 0)
                {
                    score += phrase.Contains(' ', StringComparison.Ordinal) ? 3.5 : 1.8;
                    signals.Add(phrase);
                    firstPos = Math.Min(firstPos, idx);
                    continue;
                }

                if (IsFuzzyTokenMatch(tokens, phrase))
                {
                    score += 0.8;
                    signals.Add($"fuzzy:{phrase}");
                }
            }

            if (contextualHints is { Count: > 0 })
            {
                foreach (var hint in contextualHints)
                {
                    var hn = Normalize(hint);
                    if (!string.IsNullOrWhiteSpace(hn) && signals.Any(signal => signal.Contains(hn, StringComparison.Ordinal)))
                    {
                        score += 0.3;
                    }
                }
            }

            if (HighSignalTokens.TryGetValue(pair.Key, out var highSignals))
            {
                foreach (var token in highSignals)
                {
                    if (tokens.Contains(token, StringComparer.Ordinal))
                    {
                        score += pair.Key switch
                        {
                            GuestIntent.WiFi => 1.8,
                            GuestIntent.Parking => 1.8,
                            GuestIntent.CheckIn => 1.8,
                            GuestIntent.Checkout => 1.8,
                            _ => 1.6
                        };
                        signals.Add($"token:{token}");
                    }
                }
            }

            if (score <= 0)
            {
                continue;
            }

            scoreByIntent[pair.Key] = score;
            signalsByIntent[pair.Key] = signals;
            firstPosByIntent[pair.Key] = firstPos;
        }

        var hasStrongCurrentSignals = scoreByIntent.Any(item => item.Value >= 2.6);
        var contextDependentFollowUp = IsContextDependentFollowUp(normalized, tokens);

        if (contextualHints is { Count: > 0 } && (!hasStrongCurrentSignals || contextDependentFollowUp))
        {
            foreach (var hint in contextualHints)
            {
                var inferred = InferIntentFromHint(hint);
                if (inferred is null)
                {
                    continue;
                }

                var intent = inferred.Value;
                if (!scoreByIntent.TryGetValue(intent, out var existing))
                {
                    existing = 0;
                    scoreByIntent[intent] = 0;
                }

                scoreByIntent[intent] = existing + 1.5;
                if (!signalsByIntent.TryGetValue(intent, out var hintSignals))
                {
                    hintSignals = new HashSet<string>(StringComparer.Ordinal);
                    signalsByIntent[intent] = hintSignals;
                }

                hintSignals.Add($"context:{Normalize(hint)}");
                if (!firstPosByIntent.ContainsKey(intent))
                {
                    firstPosByIntent[intent] = int.MaxValue;
                }
            }
        }

        if (scoreByIntent.Count == 0)
        {
            return new ConversationIntentResult(
                GuestIntent.Unknown,
                [],
                0.2,
                ConversationIntentConfidenceLevel.Low,
                [],
                true,
                ["I can help with Wi-Fi, check-in, checkout, parking, or house rules. What do you need?"],
                normalized);
        }

        var ranked = scoreByIntent
            .OrderByDescending(item => item.Value)
            .ThenBy(item => firstPosByIntent[item.Key])
            .ThenBy(item => item.Key.ToString(), StringComparer.Ordinal)
            .ToList();

        if (normalized.Contains(" and ", StringComparison.Ordinal) && ranked.Count > 1)
        {
            var first = ranked[0];
            var second = ranked[1];
            var closeScores = Math.Abs(first.Value - second.Value) <= 1.2;
            var secondEarlier = firstPosByIntent[second.Key] < firstPosByIntent[first.Key];
            if (closeScores && secondEarlier)
            {
                ranked[0] = second;
                ranked[1] = first;
            }
        }

        var best = ranked[0];
        if (best.Key == GuestIntent.Emergency && !emergencyMentioned)
        {
            best = ranked.FirstOrDefault(item => item.Key != GuestIntent.Emergency);
            if (best.Key == default)
            {
                best = ranked[0];
            }
        }

        var maxIntents = Math.Clamp(maximumIntents, 1, 3);
        var explicitMultiIntent = IsExplicitMultiIntentRequest(normalized, ranked, signalsByIntent);
        var selectionThreshold = explicitMultiIntent
            ? Math.Max(1.2, best.Value * 0.42)
            : Math.Max(1.6, best.Value * 0.70);

        var selectedIntents = ranked
            .Where(item => item.Value >= selectionThreshold)
            .Select(item => item.Key)
            .Where(intent => intent != GuestIntent.Unknown)
            .Distinct()
            .Take(maxIntents)
            .ToList();

        if (selectedIntents.Count == 0)
        {
            selectedIntents.Add(best.Key);
        }

        if (!emergencyMentioned)
        {
            selectedIntents.RemoveAll(intent => intent == GuestIntent.Emergency);
            if (selectedIntents.Count == 0)
            {
                selectedIntents.Add(best.Key == GuestIntent.Emergency ? GuestIntent.Unknown : best.Key);
            }
        }

        var secondScore = ranked.Count > 1 ? ranked[1].Value : 0;
        var ambiguous = !explicitMultiIntent && (selectedIntents.Count > 1 || Math.Abs(best.Value - secondScore) < 0.8);
        var confidence = NormalizeConfidence(best.Value, secondScore, ambiguous);
        var level = confidence >= 0.8
            ? ConversationIntentConfidenceLevel.High
            : confidence >= 0.55
                ? ConversationIntentConfidenceLevel.Medium
                : ConversationIntentConfidenceLevel.Low;

        var primary = selectedIntents[0];
        var secondary = selectedIntents.Skip(1).ToArray();
        var signalsOut = selectedIntents
            .SelectMany(intent => signalsByIntent.TryGetValue(intent, out var values) ? values : [])
            .Distinct(StringComparer.Ordinal)
            .Take(24)
            .ToArray();

        var clarification = BuildClarificationOptions(selectedIntents);

        return new ConversationIntentResult(
            primary,
            secondary,
            confidence,
            level,
            signalsOut,
            ambiguous,
            clarification,
            normalized);
    }

    private static IReadOnlyCollection<string> BuildClarificationOptions(IReadOnlyCollection<GuestIntent> intents)
    {
        if (intents.Count <= 1)
        {
            return [];
        }

        return intents.Take(3).Select(intent => intent switch
        {
            GuestIntent.CheckIn => "check-in time",
            GuestIntent.Checkout => "checkout time",
            GuestIntent.PropertyAccess or GuestIntent.Access => "property entry",
            GuestIntent.WiFi => "Wi-Fi access",
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
        }).ToArray();
    }

    private static double NormalizeConfidence(double best, double second, bool ambiguous)
    {
        var spread = Math.Max(0, best - second);
        var normalized = Math.Clamp((best / 7.5) + (spread / 8.0), 0.15, 0.98);
        return ambiguous ? Math.Min(0.62, normalized) : normalized;
    }

    private static int IndexOfPhrase(string haystack, string phrase)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(phrase))
        {
            return -1;
        }

        var boundedHaystack = $" {haystack} ";
        var boundedNeedle = $" {phrase} ";
        return boundedHaystack.IndexOf(boundedNeedle, StringComparison.Ordinal);
    }

    private static bool ContainsPhrase(string haystack, string phrase)
    {
        return IndexOfPhrase(haystack, Normalize(phrase)) >= 0;
    }

    private static bool ContainsAnyPhrase(string haystack, IReadOnlyCollection<string> phrases)
    {
        return phrases.Any(phrase => ContainsPhrase(haystack, phrase));
    }

    private static bool IsDirectPropertyEntryRequest(string normalized)
    {
        return ContainsPhrase(normalized, "how do i enter")
            || ContainsPhrase(normalized, "how do i get in")
            || ContainsPhrase(normalized, "how do i get inside")
            || ContainsPhrase(normalized, "how to enter")
            || ContainsPhrase(normalized, "comment entrer");
    }

    private static bool IsContextDependentFollowUp(string normalized, IReadOnlyCollection<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (tokens.Count <= 5)
        {
            if (ContainsPhrase(normalized, "is it")
                || ContainsPhrase(normalized, "is that")
                || ContainsPhrase(normalized, "can i")
                || ContainsPhrase(normalized, "what about")
                || ContainsPhrase(normalized, "how about")
                || ContainsPhrase(normalized, "and that")
                || ContainsPhrase(normalized, "what s the password")
                || ContainsPhrase(normalized, "whats the password"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExplicitMultiIntentRequest(
        string normalized,
        IReadOnlyCollection<KeyValuePair<GuestIntent, double>> ranked,
        IReadOnlyDictionary<GuestIntent, HashSet<string>> signalsByIntent)
    {
        var hasConjunction = normalized.Contains(" and ", StringComparison.Ordinal)
            || normalized.Contains(",", StringComparison.Ordinal)
            || normalized.Contains(" also ", StringComparison.Ordinal);
        if (!hasConjunction)
        {
            return false;
        }

        var strongIntentCount = ranked
            .Take(3)
            .Count(item => item.Value >= 2.2
                && signalsByIntent.TryGetValue(item.Key, out var signals)
                && signals.Any(signal => !signal.StartsWith("context:", StringComparison.Ordinal)));

        return strongIntentCount >= 2;
    }

    private static bool IsFuzzyTokenMatch(IReadOnlyCollection<string> tokens, string phrase)
    {
        if (phrase.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        if (phrase.Length < 4)
        {
            return false;
        }

        foreach (var token in tokens)
        {
            if (token.Length < 5)
            {
                continue;
            }

            var distance = LevenshteinDistance(token, phrase);
            var maxAllowed = token.Length >= 8 ? 2 : 1;
            if (distance <= maxAllowed)
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length + 8);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalizedChar = ch switch
            {
                '\u2019' or '\u2018' or '\'' => ' ',
                '\u2013' or '\u2014' or '-' => ' ',
                _ => char.ToLowerInvariant(ch)
            };

            builder.Append(char.IsLetterOrDigit(normalizedChar) ? normalizedChar : ' ');
        }

        var normalized = string.Join(' ', builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        normalized = normalized
            .Replace("wi fi", "wifi", StringComparison.Ordinal)
            .Replace("wifii", "wifi", StringComparison.Ordinal)
            .Replace("check in", "checkin", StringComparison.Ordinal)
            .Replace("chek in", "checkin", StringComparison.Ordinal)
            .Replace("houze rules", "house rules", StringComparison.Ordinal)
            .Replace("entering", "enter", StringComparison.Ordinal)
            .Replace("getting in", "get in", StringComparison.Ordinal)
            .Replace("getting inside", "get inside", StringComparison.Ordinal)
            .Replace("check out", "checkout", StringComparison.Ordinal);

        normalized = normalized
            .Replace("chekout", "checkout", StringComparison.Ordinal);

        return normalized;
    }

    private static GuestIntent? InferIntentFromHint(string hint)
    {
        var normalized = Normalize(hint);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Contains("wifi", StringComparison.Ordinal) || normalized.Contains("internet", StringComparison.Ordinal))
        {
            return GuestIntent.WiFi;
        }

        if (normalized.Contains("parking", StringComparison.Ordinal) || normalized.Contains("garage", StringComparison.Ordinal))
        {
            return GuestIntent.Parking;
        }

        if (normalized.Contains("checkout", StringComparison.Ordinal) || normalized.Contains("depart", StringComparison.Ordinal))
        {
            return GuestIntent.Checkout;
        }

        if (normalized.Contains("checkin", StringComparison.Ordinal) || normalized.Contains("arrival", StringComparison.Ordinal))
        {
            return GuestIntent.CheckIn;
        }

        if (normalized.Contains("access", StringComparison.Ordinal) || normalized.Contains("door", StringComparison.Ordinal) || normalized.Contains("entry", StringComparison.Ordinal))
        {
            return GuestIntent.PropertyAccess;
        }

        if (normalized.Contains("house rule", StringComparison.Ordinal) || normalized.Contains("pet", StringComparison.Ordinal))
        {
            return GuestIntent.HouseRules;
        }

        return null;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var rows = a.Length + 1;
        var cols = b.Length + 1;
        var dp = new int[rows, cols];

        for (var i = 0; i < rows; i++)
        {
            dp[i, 0] = i;
        }

        for (var j = 0; j < cols; j++)
        {
            dp[0, j] = j;
        }

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[rows - 1, cols - 1];
    }
}
