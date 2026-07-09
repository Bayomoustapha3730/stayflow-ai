using StayFlow.Api.DTOs.ReservationContext;

namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIContextBuildResult
{
    public AIContextBuildOutcome Outcome { get; init; }
    public AIContext? Context { get; init; }
    public IReadOnlyCollection<ReservationCandidateLabel> CandidateLabels { get; init; } = [];
    public IReadOnlyCollection<QuestionContextCategory> QuestionCategories { get; init; } = [];
    public string? EscalationReason { get; init; }
    public string? Message { get; init; }
}
