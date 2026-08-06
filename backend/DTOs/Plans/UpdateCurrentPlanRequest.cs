namespace StayFlow.Api.DTOs.Plans;

public sealed class UpdateCurrentPlanRequest
{
    public Guid? PlanId { get; init; }
    public string? PlanName { get; init; }
    public string? Notes { get; init; }
}