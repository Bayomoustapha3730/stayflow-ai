namespace StayFlow.Api.Services.AI.Intent;

public sealed record ConversationIntentResult(
    GuestIntent PrimaryIntent,
    IReadOnlyCollection<GuestIntent> SecondaryIntents,
    double Confidence,
    ConversationIntentConfidenceLevel ConfidenceLevel,
    IReadOnlyCollection<string> MatchedSignals,
    bool IsAmbiguous,
    IReadOnlyCollection<string> ClarificationOptions,
    string NormalizedQuery)
{
    public GuestIntentResult ToGuestIntentResult()
    {
        return new GuestIntentResult(
            PrimaryIntent,
            Confidence,
            MatchedSignals,
            IsAmbiguous,
            IsAmbiguous
                ? "Multiple intents matched with similar confidence."
                : $"Matched deterministic signals for {PrimaryIntent}.");
    }

    public IReadOnlyCollection<GuestIntent> AllIntents()
    {
        return new[] { PrimaryIntent }
            .Concat(SecondaryIntents)
            .Distinct()
            .ToArray();
    }
}
