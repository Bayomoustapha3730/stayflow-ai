namespace StayFlow.Api.Services.AI.Intent;

public interface IConversationIntentRecognizer
{
    ConversationIntentResult Recognize(
        string query,
        IReadOnlyCollection<string>? contextualHints = null,
        int maximumIntents = 3);
}
