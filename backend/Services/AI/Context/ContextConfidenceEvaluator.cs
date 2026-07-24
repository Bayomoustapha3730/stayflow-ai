namespace StayFlow.Api.Services.AI.Context;

public sealed class ContextConfidenceEvaluator : IContextConfidenceEvaluator
{
    public ContextConfidenceResult Evaluate(ConversationContext context)
    {
        var score = 100;
        var reasons = new List<string>();
        var missingContext = new HashSet<ConversationContextWarning>();

        if (!context.PropertyId.HasValue)
        {
            score -= 30;
            reasons.Add("Property context is unavailable.");
            missingContext.Add(ConversationContextWarning.MissingProperty);
        }

        if (!context.ReservationId.HasValue)
        {
            score -= 20;
            reasons.Add("Reservation context is unavailable.");
            missingContext.Add(ConversationContextWarning.MissingReservation);
        }

        if (context.ApprovedKnowledgeItems.Count == 0)
        {
            score -= 20;
            reasons.Add("No approved property knowledge is available.");
            missingContext.Add(ConversationContextWarning.NoApprovedKnowledge);
        }

        var guestMessages = context.VisibleMessages
            .Where(message => string.Equals(message.SenderType, "Guest", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (guestMessages.Count == 0)
        {
            score -= 25;
            reasons.Add("No visible guest messages are available.");
            missingContext.Add(ConversationContextWarning.NoVisibleMessages);
        }

        if (context.Truncated)
        {
            score -= 5;
            reasons.Add("Context was truncated to stay within safety limits.");
            missingContext.Add(ConversationContextWarning.ContextTruncated);
        }

        if (HasAmbiguousLatestGuestRequest(guestMessages))
        {
            score -= 10;
            reasons.Add("The latest guest request appears ambiguous.");
            missingContext.Add(ConversationContextWarning.AmbiguousGuestRequest);
        }

        if (HasConflictingKnowledge(context.ApprovedKnowledgeItems))
        {
            score -= 20;
            reasons.Add("Approved knowledge contains conflicting guidance.");
            missingContext.Add(ConversationContextWarning.ConflictingKnowledge);
        }

        score = Math.Clamp(score, 0, 100);
        var level = score >= 80
            ? ContextConfidenceLevel.High
            : score >= 50
                ? ContextConfidenceLevel.Medium
                : ContextConfidenceLevel.Low;

        if (reasons.Count == 0)
        {
            reasons.Add("All required context sections are available and coherent.");
        }

        return new ContextConfidenceResult(score, level, reasons, missingContext.ToList());
    }

    private static bool HasAmbiguousLatestGuestRequest(IReadOnlyList<ConversationContextVisibleMessage> guestMessages)
    {
        if (guestMessages.Count == 0)
        {
            return false;
        }

        var latest = guestMessages[^1].Text.Trim();
        if (latest.Length == 0 || latest.Length < 12)
        {
            return true;
        }

        var lower = latest.ToLowerInvariant();
        var ambiguousPhrases = new[] { "this", "that", "it", "same", "as above", "pls help", "help" };

        return ambiguousPhrases.Any(phrase => string.Equals(lower, phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasConflictingKnowledge(IReadOnlyCollection<ConversationContextKnowledgeItem> knowledgeItems)
    {
        return knowledgeItems
            .GroupBy(item => $"{item.Category}:{item.Title.Trim()}".ToLowerInvariant())
            .Any(group => group
                .Select(item => NormalizeForComparison(item.Content))
                .Distinct(StringComparer.Ordinal)
                .Count() > 1);
    }

    private static string NormalizeForComparison(string content)
    {
        return string.Join(' ', content
            .Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
    }
}
