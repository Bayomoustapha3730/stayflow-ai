namespace StayFlow.Api.Services.AI.Orchestration;

public interface IDeterministicConciergeResponseGenerator
{
    ConciergeResponseResult Generate(ConciergeResponseRequest request);
}