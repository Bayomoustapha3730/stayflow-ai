using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIResponseValidation;
using StayFlow.Api.DTOs.ReservationContext;

namespace StayFlow.Api.DTOs.AIOrchestration;

public sealed class AIOrchestrationResult
{
    public AIOrchestrationOutcome Outcome { get; init; }
    public string GuestSafeMessage { get; init; } = string.Empty;
    public IReadOnlyCollection<ReservationCandidateLabel> CandidateLabels { get; init; } = [];
    public IReadOnlyCollection<QuestionContextCategory> QuestionCategories { get; init; } = [];
    public IReadOnlyCollection<AIResponseViolationCode> ValidationViolations { get; init; } = [];
    public AIProviderMetadata? ProviderMetadata { get; init; }
}
