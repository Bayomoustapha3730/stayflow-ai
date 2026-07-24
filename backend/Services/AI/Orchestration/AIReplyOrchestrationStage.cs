namespace StayFlow.Api.Services.AI.Orchestration;

public enum AIReplyOrchestrationStage
{
    RequestValidated = 0,
    ContextLoaded = 1,
    IntentDetected = 2,
    KnowledgeRanked = 3,
    PromptBuilt = 4,
    ProviderInvoked = 5,
    OutputValidated = 6,
    SafetyEvaluated = 7,
    OutputNormalized = 8,
    ConfidenceEvaluated = 9,
    SourcesAssembled = 10,
    FallbackApplied = 11,
    ResultAssembled = 12
}
