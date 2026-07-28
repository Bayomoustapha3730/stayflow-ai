using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class DeterministicKnowledgeSemanticSimilarityService(
    IKnowledgeEmbeddingProvider embeddingProvider,
    IOptions<KnowledgeEmbeddingOptions> options) : IKnowledgeSemanticSimilarityService
{
    public double Score(
        ConversationContextKnowledgeItem item,
        ConversationIntentResult intentResult,
        KnowledgeQueryExpansionResult expansion)
    {
        var candidate = string.Join(' ', new[]
        {
            item.Title,
            item.Summary ?? string.Empty,
            item.Content,
            string.Join(' ', item.Tags)
        });

        var candidateTerms = Tokenize(candidate).ToHashSet(StringComparer.Ordinal);
        if (candidateTerms.Count == 0 || expansion.ExpandedTerms.Count == 0)
        {
            return 0;
        }

        var overlap = expansion.ExpandedTerms.Count(term => candidateTerms.Contains(term));
        var jaccard = (double)overlap / Math.Max(1, expansion.ExpandedTerms.Count + candidateTerms.Count - overlap);

        var phraseBoost = expansion.MatchedPhrases.Count(phrase => Normalize(candidate).Contains(Normalize(phrase), StringComparison.Ordinal)) * 0.08;

        var baseScore = Math.Clamp(jaccard + phraseBoost, 0, 1);
        var embeddingOptions = options.Value;
        if (!embeddingOptions.EnableEmbeddingBlend)
        {
            return baseScore;
        }

        var queryText = string.Join(' ', expansion.ExpandedTerms);
        var queryEmbedding = embeddingProvider.CreateEmbedding(queryText);
        var candidateEmbedding = embeddingProvider.CreateEmbedding(candidate);
        if (!queryEmbedding.Success || !candidateEmbedding.Success)
        {
            return baseScore;
        }

        var vectorSimilarity = CosineSimilarity(queryEmbedding.Vector, candidateEmbedding.Vector);
        if (vectorSimilarity <= 0)
        {
            return baseScore;
        }

        var weight = Math.Clamp(embeddingOptions.EmbeddingWeight, 0, 0.5);
        return Math.Clamp((baseScore * (1 - weight)) + (vectorSimilarity * weight), 0, 1);
    }

    private static double CosineSimilarity(IReadOnlyCollection<double> left, IReadOnlyCollection<double> right)
    {
        if (left.Count == 0 || right.Count == 0 || left.Count != right.Count)
        {
            return 0;
        }

        var leftVector = left.ToArray();
        var rightVector = right.ToArray();
        var dot = 0d;
        var leftNorm = 0d;
        var rightNorm = 0d;

        for (var i = 0; i < leftVector.Length; i++)
        {
            dot += leftVector[i] * rightVector[i];
            leftNorm += leftVector[i] * leftVector[i];
            rightNorm += rightVector[i] * rightVector[i];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return Math.Clamp(dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)), 0, 1);
    }

    private static IEnumerable<string> Tokenize(string input)
    {
        return Normalize(input)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.EndsWith('s') && token.Length > 3 ? token[..^1] : token)
            .Distinct(StringComparer.Ordinal);
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var chars = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lowered = char.ToLowerInvariant(ch);
            chars.Append(char.IsLetterOrDigit(lowered) ? lowered : ' ');
        }

        return string.Join(' ', chars.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
