namespace StayFlow.Api.Services.AI.Retrieval;

public sealed record KnowledgeQueryExpansionResult(
    string NormalizedQuery,
    IReadOnlyCollection<string> Tokens,
    IReadOnlyCollection<string> MatchedPhrases,
    IReadOnlyCollection<string> IntentSynonyms,
    IReadOnlyCollection<string> ExpandedTerms,
    IReadOnlyCollection<string> ExcludedEmergencyTerms);
