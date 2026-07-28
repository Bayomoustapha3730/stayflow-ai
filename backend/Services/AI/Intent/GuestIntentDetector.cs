using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Intent;

public sealed class GuestIntentDetector(IConversationIntentRecognizer? recognizer = null) : IGuestIntentDetector
{
    private readonly IConversationIntentRecognizer recognizer = recognizer ?? new ConversationIntentRecognizer();

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

        var query = string.Join(' ', latestGuestMessages.Select(message => message.Text));
        var result = recognizer.Recognize(query, maximumIntents: 3);
        return result.ToGuestIntentResult();
    }
}
