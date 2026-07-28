namespace StayFlow.Api.Services.AI.Orchestration;

public interface IConciergeResponseGenerator
{
    ConciergeResponseResult Generate(ConciergeResponseRequest request);
}
