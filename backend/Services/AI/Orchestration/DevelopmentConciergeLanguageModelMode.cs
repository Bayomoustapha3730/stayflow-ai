namespace StayFlow.Api.Services.AI.Orchestration;

public enum DevelopmentConciergeLanguageModelMode
{
    Success = 0,
    Timeout = 1,
    Exception = 2,
    Empty = 3,
    HallucinatedFact = 4,
    InvalidSource = 5,
    PromptLeak = 6,
    MissingMultiIntentAnswer = 7
}