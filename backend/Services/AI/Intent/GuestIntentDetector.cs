using System.Text;
using System.Text.RegularExpressions;
using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Intent;

public sealed class GuestIntentDetector : IGuestIntentDetector
{
    private static readonly IReadOnlyDictionary<GuestIntent, string[]> IntentTerms =
        new Dictionary<GuestIntent, string[]>
        {
            [GuestIntent.WiFi] = ["wifi", "wi fi", "wi-fi", "internet", "network"],
            [GuestIntent.CheckIn] = ["check in", "check-in", "arrival", "arrive"],
            [GuestIntent.Checkout] = ["check out", "checkout", "check-out", "departure", "depart"],
            [GuestIntent.Parking] = ["parking", "car", "garage", "park"],
            [GuestIntent.HouseRules] = ["house rules", "quiet hours", "smoking", "rule"],
            [GuestIntent.Amenities] = ["amenities", "pool", "gym", "kitchen", "facility"],
            [GuestIntent.Laundry] = ["laundry", "washer", "dryer"],
            [GuestIntent.Thermostat] = ["thermostat", "temperature", "ac", "aircon", "heating"],
            [GuestIntent.Trash] = ["trash", "garbage", "waste", "bin"],
            [GuestIntent.Emergency] = ["emergency", "ambulance", "police", "fire", "urgent"],
            [GuestIntent.Accessibility] = ["accessibility", "wheelchair", "elevator", "lift", "accessible"],
            [GuestIntent.Maintenance] = ["maintenance", "broken", "not working", "repair", "fix", "door", "lock", "key", "access code"],
            [GuestIntent.Noise] = ["noise", "loud", "neighbour", "neighbor", "disturbance"],
            [GuestIntent.Refund] = ["refund", "money back", "reimburse"],
            [GuestIntent.Cancellation] = ["cancel", "cancellation"],
            [GuestIntent.ReservationChange] = ["change booking", "modify booking", "change reservation", "extend", "shorten"],
            [GuestIntent.LateArrival] = ["late arrival", "arrive late", "coming late"],
            [GuestIntent.EarlyCheckIn] = ["early check in", "early check-in", "check in early", "check-in early"],
            [GuestIntent.GeneralQuestion] = ["help", "question", "info", "information"]
        };

    public GuestIntentResult Detect(ConversationContext context)
    {
        var latestGuestMessages = context.VisibleMessages
            .Where(message => string.Equals(message.SenderType, "Guest", StringComparison.OrdinalIgnoreCase))
            .TakeLast(3)
            .ToList();

        if (latestGuestMessages.Count == 0)
        {
            return new GuestIntentResult(
                GuestIntent.Unknown,
                0,
                [],
                true,
                "No guest-visible messages were available to detect intent.");
        }

        var normalized = NormalizeText(string.Join(' ', latestGuestMessages.Select(message => message.Text)));
        var ranked = new List<(GuestIntent Intent, int Score, HashSet<string> MatchedTerms)>();

        foreach (var pair in IntentTerms)
        {
            var score = 0;
            var matchedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var term in pair.Value)
            {
                if (!ContainsTerm(normalized, term))
                {
                    continue;
                }

                matchedTerms.Add(term);
                score += 2;
                if (normalized.Contains($" {term} ", StringComparison.Ordinal))
                {
                    score += 1;
                }
            }

            if (score > 0)
            {
                ranked.Add((pair.Key, score, matchedTerms));
            }
        }

        if (ranked.Count == 0)
        {
            return new GuestIntentResult(
                GuestIntent.Unknown,
                0.2,
                [],
                true,
                "No supported deterministic intent terms were detected.");
        }

        var ordered = ranked
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Intent.ToString(), StringComparer.Ordinal)
            .ToList();

        var best = ordered[0];
        var second = ordered.Count > 1 ? ordered[1] : (Intent: GuestIntent.Unknown, Score: -1, MatchedTerms: new HashSet<string>());
        var ambiguous = ordered.Count > 1 && Math.Abs(best.Score - second.Score) <= 1;

        var confidence = best.Score switch
        {
            >= 6 => 0.92,
            >= 4 => 0.78,
            >= 2 => 0.62,
            _ => 0.45
        };

        if (ambiguous)
        {
            confidence = Math.Min(confidence, 0.55);
        }

        return new GuestIntentResult(
            best.Intent,
            confidence,
            best.MatchedTerms.OrderBy(term => term, StringComparer.Ordinal).ToArray(),
            ambiguous,
            ambiguous
                ? $"Multiple intent categories matched with similar confidence ({best.Intent} and {second.Intent})."
                : $"Matched deterministic terms for {best.Intent}.");
    }

    private static bool ContainsTerm(string normalized, string term)
    {
        return normalized.Contains($" {term} ", StringComparison.Ordinal);
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return " ";
        }

        var lower = value.ToLowerInvariant();
        var builder = new StringBuilder(lower.Length + 2);
        builder.Append(' ');

        foreach (var ch in lower)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }

        builder.Append(' ');
        var squashed = Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
        return $" {squashed} ";
    }
}
