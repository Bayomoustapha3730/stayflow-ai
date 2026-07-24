using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class AIReplyFallbackProvider : IAIReplyFallbackProvider
{
    public string BuildFallback(
        AIReplyOperation operation,
        string? tone,
        GuestIntentResult? intent,
        bool includeReviewReminder)
    {
        if (operation == AIReplyOperation.SuggestedHostReplies)
        {
            var baseLine = ToneMessage(tone);
            return includeReviewReminder
                ? $"{baseLine} Please verify details before sending."
                : baseLine;
        }

        var fallback = ToneMessage(tone);
        if (operation == AIReplyOperation.FutureGuestReply)
        {
            return "I received your message and I am routing this to the host team for verification before any action is taken.";
        }

        if (intent is { Intent: GuestIntent.Emergency })
        {
            return "Thank you for your message. Please contact local emergency services if there is immediate danger while I notify the host team right away.";
        }

        return includeReviewReminder
            ? $"{fallback} Please verify details before sending."
            : fallback;
    }

    private static string ToneMessage(string? tone)
    {
        return tone?.Trim().ToLowerInvariant() switch
        {
            "friendly" => "Thanks for the message! I\'m checking this for you and will update you shortly.",
            "luxury" => "Thank you for bringing this to our attention. I\'m reviewing the details and will be pleased to update you shortly.",
            "casual" => "Thanks for the message. I\'m checking on that now and will get back to you shortly.",
            _ => "Thank you for reaching out. I\'m reviewing the details and will provide an update shortly."
        };
    }
}
