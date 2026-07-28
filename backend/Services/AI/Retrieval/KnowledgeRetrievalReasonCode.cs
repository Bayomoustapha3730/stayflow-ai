namespace StayFlow.Api.Services.AI.Retrieval;

public enum KnowledgeRetrievalReasonCode
{
    ExactIntentAndCategoryMatch = 0,
    StrongIntentMatch = 1,
    StrongLexicalMatch = 2,
    StrongSemanticMatch = 3,
    MultiSignalMatch = 4,
    AmbiguousTopCandidates = 5,
    MissingKnowledgeForIntent = 6,
    UnsupportedQuestion = 7,
    HumanTakeover = 8,
    ProviderUnavailable = 9,

    // Legacy reason codes retained for compatibility.
    ExactTitleMatch = 0,
    CategoryAndKeywordMatch = 1,
    StrongKeywordMatch = 2,
    TagMatch = 3,
    SemanticMatch = 4,
    WeakMatch = 5,
    NoMatch = 6,
    Ambiguous = 7,
    EmergencyIntent = 8,
    RestrictedByPolicy = 9
}
