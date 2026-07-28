using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Memory;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed record KnowledgeRetrievalRequest(
    Guid CompanyId,
    Guid? PropertyId,
    Guid ConversationId,
    string Query,
    ConversationIntentResult IntentResult,
    ConversationMemoryContext MemoryContext,
    int MaximumCandidates,
    int MaximumSelectedItems,
    int ContextCharacterBudget);
