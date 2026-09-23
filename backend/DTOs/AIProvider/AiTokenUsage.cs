namespace StayFlow.Api.DTOs.AIProvider;

public sealed record AiTokenUsage(
    long InputTokens,
    long OutputTokens,
    long TotalTokens);
