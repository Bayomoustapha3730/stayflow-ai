using Microsoft.Extensions.Options;
using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Retrieval;

public sealed class DeterministicKnowledgeReranker(IOptions<KnowledgeRerankerOptions> options) : IKnowledgeReranker
{
    public IReadOnlyCollection<KnowledgeRetrievalCandidate> Rerank(
        IReadOnlyCollection<KnowledgeRetrievalCandidate> candidates,
        ConversationContext context,
        KnowledgeRetrievalRequest request,
        int maxCandidates)
    {
        var rerankerOptions = options.Value;
        var boundedMax = Math.Clamp(maxCandidates, 1, 100);
        if (!rerankerOptions.Enabled)
        {
            return candidates
                .OrderByDescending(candidate => candidate.FinalScore)
                .ThenByDescending(candidate => candidate.SemanticScore)
                .ThenByDescending(candidate => candidate.LexicalScore)
                .ThenBy(candidate => candidate.Item.Title, StringComparer.Ordinal)
                .Take(boundedMax)
                .Select((candidate, idx) => candidate with { Rank = idx + 1 })
                .ToList();
        }

        var priorSelections = request.MemoryContext.PriorSelectedArticleIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pendingClarification = request.MemoryContext.PendingClarificationContext?.Prompt;

        var reranked = candidates
            .Select(candidate =>
            {
                var score = candidate.FinalScore;

                if (priorSelections.Contains(candidate.ArticleId))
                {
                    score += rerankerOptions.PriorSelectionBoost;
                }

                if (!string.IsNullOrWhiteSpace(pendingClarification)
                    && candidate.Item.Title.Contains(pendingClarification, StringComparison.OrdinalIgnoreCase))
                {
                    score += rerankerOptions.ClarificationTopicBoost;
                }

                return candidate with
                {
                    Score = score,
                    FinalScore = score
                };
            })
            .OrderByDescending(candidate => candidate.FinalScore)
            .ThenByDescending(candidate => candidate.SemanticScore)
            .ThenByDescending(candidate => candidate.LexicalScore)
            .ThenBy(candidate => candidate.Item.Title, StringComparer.Ordinal)
            .Take(boundedMax)
            .Select((candidate, idx) => candidate with { Rank = idx + 1 })
            .ToList();

        return reranked;
    }
}