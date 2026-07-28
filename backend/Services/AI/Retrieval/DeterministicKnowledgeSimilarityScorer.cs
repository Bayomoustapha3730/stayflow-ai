using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class DeterministicKnowledgeSimilarityScorer : IKnowledgeSimilarityScorer
{
    public double Score(
        ConversationContextKnowledgeItem item,
        GuestIntentResult intent,
        string normalizedQuery,
        IReadOnlyCollection<string> queryTokens)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var candidateTokens = Tokenize(item.Title)
            .Concat(Tokenize(item.Summary ?? string.Empty))
            .Concat(Tokenize(item.Content))
            .Concat(item.Tags.SelectMany(Tokenize))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        if (candidateTokens.Count == 0)
        {
            return 0;
        }

        var overlap = queryTokens.Count(token => candidateTokens.Contains(token));
        if (overlap == 0)
        {
            return 0;
        }

        var denominator = Math.Max(1, queryTokens.Count);
        return Math.Clamp((double)overlap / denominator, 0, 1);
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        var normalized = string.Concat(value
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' '));

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.EndsWith('s') && token.Length > 3 ? token[..^1] : token);
    }
}
