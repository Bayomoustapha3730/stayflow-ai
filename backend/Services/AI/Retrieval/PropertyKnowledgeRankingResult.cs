using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed record PropertyKnowledgeRankingResult(
    IReadOnlyCollection<PropertyKnowledgeCandidate> RankedItems,
    IReadOnlyCollection<ConversationContextKnowledgeItem> SelectedItems,
    int RejectedItemsCount,
    bool Ambiguous,
    IReadOnlyCollection<string> Reasons);
