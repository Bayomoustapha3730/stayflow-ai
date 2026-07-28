namespace StayFlow.Api.Services.AI.Orchestration;

public interface IConciergeLanguageModelProviderFactory
{
    IConciergeLanguageModel GetProvider();
}