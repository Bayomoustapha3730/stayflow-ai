using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Memory;
using StayFlow.Api.Services.AI.Retrieval;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed record ConciergeLanguageModelRequest(
    string GuestQuestion,
    ConversationIntentResult IntentResult,
    KnowledgeRetrievalResult RetrievalResult,
    ConversationMemoryContext MemoryContext,
    ConciergeRequiredOutcome RequiredOutcome,
    string Language,
    string? PropertyName,
    string? ReservationContext,
    ReservationPaymentGroundingDto? PaymentGrounding,
    ConciergeTone Tone,
    bool HumanTakeoverState,
    bool IsEmergency,
    bool IsClosedConversation,
    string PromptPolicyVersion,
    int MaximumResponseCharacters,
    int MaximumKnowledgeCharacters);