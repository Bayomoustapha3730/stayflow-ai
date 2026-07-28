using StayFlow.Api.Services.AI.Memory;
using StayFlow.Api.Services.AI.Retrieval;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed record ConciergeResponseRequest(
    string GuestQuestion,
    ConversationIntentResult IntentResult,
    KnowledgeRetrievalResult RetrievalResult,
    ConversationMemoryContext MemoryContext,
    string? PropertyName,
    string? ReservationContext,
    ConciergeTone Tone,
    string Language,
    bool HumanTakeoverState);
