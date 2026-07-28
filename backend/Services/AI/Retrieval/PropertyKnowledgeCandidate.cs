using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed record PropertyKnowledgeCandidate(
    ConversationContextKnowledgeItem Item,
    int Score,
    IReadOnlyCollection<string> Reasons);
