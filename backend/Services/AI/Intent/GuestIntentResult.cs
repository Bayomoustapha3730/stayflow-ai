namespace StayFlow.Api.Services.AI.Intent;

public sealed record GuestIntentResult(
    GuestIntent Intent,
    double ConfidenceScore,
    IReadOnlyCollection<string> MatchedTerms,
    bool Ambiguous,
    string Explanation);
