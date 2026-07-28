namespace StayFlow.Api.Services.AI.Memory;

public sealed record PendingClarificationContext(
    string Prompt,
    IReadOnlyCollection<string> Choices,
    DateTimeOffset CreatedAtUtc);