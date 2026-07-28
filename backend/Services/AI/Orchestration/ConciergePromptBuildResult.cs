namespace StayFlow.Api.Services.AI.Orchestration;

public sealed record ConciergePromptBuildResult(
    string SystemPrompt,
    string UserPrompt,
    int KnowledgeCharacters,
    IReadOnlyCollection<string> SourceArticleIds,
    IReadOnlyCollection<string> WarningCodes);