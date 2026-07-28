using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Memory;

public sealed class ConversationMemoryService(
    IConversationIntentRecognizer intentRecognizer,
    IConversationSummaryService? summaryService = null) : IConversationMemoryService
{
    private readonly IConversationSummaryService summaryService = summaryService ?? new DeterministicConversationSummaryService();

    public ConversationMemoryContext BuildContext(
        ConversationContext context,
        int recentMessageCount,
        int characterBudget,
        IReadOnlyCollection<string>? priorSelectedArticleIds = null,
        string? pendingClarification = null)
    {
        var maxMessages = Math.Clamp(recentMessageCount, 4, 20);
        var maxChars = Math.Max(300, characterBudget);

        var visible = context.VisibleMessages
            .OrderBy(message => message.TimestampUtc)
            .TakeLast(maxMessages)
            .ToList();

        var wasTruncated = context.VisibleMessages.Count > visible.Count;
        var user = new List<string>();
        var assistant = new List<string>();
        var runningChars = 0;

        foreach (var msg in visible.AsEnumerable().Reverse())
        {
            if (runningChars >= maxChars)
            {
                wasTruncated = true;
                break;
            }

            var text = msg.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (runningChars + text.Length > maxChars)
            {
                text = text[..Math.Max(0, maxChars - runningChars)];
                wasTruncated = true;
            }

            runningChars += text.Length;

            if (string.Equals(msg.SenderType, "Guest", StringComparison.OrdinalIgnoreCase))
            {
                user.Insert(0, text);
            }
            else if (string.Equals(msg.SenderType, "AI", StringComparison.OrdinalIgnoreCase)
                || string.Equals(msg.SenderType, "Host", StringComparison.OrdinalIgnoreCase))
            {
                assistant.Insert(0, text);
            }
        }

        GuestIntent? lastIntent = null;
        if (user.Count > 0)
        {
            for (var i = user.Count - 1; i >= 0; i--)
            {
                var detected = intentRecognizer.Recognize(user[i]);
                if (detected.PrimaryIntent == GuestIntent.Unknown)
                {
                    continue;
                }

                lastIntent = detected.PrimaryIntent;
                break;
            }
        }

        var topic = lastIntent?.ToString();
        var pendingClarificationContext = string.IsNullOrWhiteSpace(pendingClarification)
            ? null
            : new PendingClarificationContext(
                pendingClarification.Trim(),
                [],
                DateTimeOffset.UtcNow);
        var summary = summaryService.BuildSummary(user, assistant, lastIntent, pendingClarificationContext);

        return new ConversationMemoryContext(
            user,
            assistant,
            lastIntent,
            topic,
            priorSelectedArticleIds ?? [],
            pendingClarification,
            pendingClarificationContext,
            new Dictionary<string, string>(StringComparer.Ordinal),
            summary,
            wasTruncated,
            DateTimeOffset.UtcNow);
    }
}
