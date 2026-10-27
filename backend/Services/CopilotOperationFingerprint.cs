using System.Security.Cryptography;
using System.Text.Json;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public static class CopilotOperationFingerprint
{
    private const int Version = 1;

    public static string Suggestions(Guid conversationId, string? tone)
        => Compute(CopilotOperationType.CopilotSuggestion, conversationId, tone, null, null, 3);

    public static string GeneratedReply(Guid conversationId, string? tone, string? hostDraft, string? guidance)
        => Compute(CopilotOperationType.CopilotGeneratedReply, conversationId, tone, hostDraft, guidance, null);

    public static string WorkspaceDraft(Guid conversationId, string? tone, string? hostInstruction)
        => Compute(CopilotOperationType.WorkspaceDraft, conversationId, tone, null, hostInstruction, null);

    private static string Compute(
        CopilotOperationType operationType,
        Guid conversationId,
        string? tone,
        string? hostDraft,
        string? instruction,
        int? suggestionCount)
    {
        var canonicalRequest = new CanonicalRequest(
            Version,
            operationType,
            conversationId,
            tone,
            hostDraft,
            instruction,
            suggestionCount);
        var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonicalRequest));
        return Convert.ToHexStringLower(digest);
    }

    private sealed record CanonicalRequest(
        int Version,
        CopilotOperationType OperationType,
        Guid ConversationId,
        string? Tone,
        string? HostDraft,
        string? Instruction,
        int? SuggestionCount);
}
