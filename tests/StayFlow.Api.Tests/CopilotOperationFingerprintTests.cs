using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CopilotOperationFingerprintTests
{
    [Fact]
    public void SuggestionsFingerprint_IsStableAndOnlyChangesForSemanticInputs()
    {
        var conversationId = Guid.NewGuid();

        var first = CopilotOperationFingerprint.Suggestions(conversationId, "professional");
        var same = CopilotOperationFingerprint.Suggestions(conversationId, "professional");
        var changedTone = CopilotOperationFingerprint.Suggestions(conversationId, "friendly");

        Assert.Equal(64, first.Length);
        Assert.Equal(first, same);
        Assert.NotEqual(first, changedTone);
    }

    [Fact]
    public void GeneratedReplyFingerprint_CoversDraftGuidanceToneAndConversation()
    {
        var conversationId = Guid.NewGuid();
        var original = CopilotOperationFingerprint.GeneratedReply(conversationId, "professional", "Draft", "Guidance");

        Assert.NotEqual(original, CopilotOperationFingerprint.GeneratedReply(conversationId, "friendly", "Draft", "Guidance"));
        Assert.NotEqual(original, CopilotOperationFingerprint.GeneratedReply(conversationId, "professional", "Changed draft", "Guidance"));
        Assert.NotEqual(original, CopilotOperationFingerprint.GeneratedReply(conversationId, "professional", "Draft", "Changed guidance"));
        Assert.NotEqual(original, CopilotOperationFingerprint.GeneratedReply(Guid.NewGuid(), "professional", "Draft", "Guidance"));
    }

    [Fact]
    public void WorkspaceDraftFingerprint_CoversToneInstructionAndConversation()
    {
        var conversationId = Guid.NewGuid();
        var original = CopilotOperationFingerprint.WorkspaceDraft(conversationId, "professional", "Instruction");

        Assert.NotEqual(original, CopilotOperationFingerprint.WorkspaceDraft(conversationId, "friendly", "Instruction"));
        Assert.NotEqual(original, CopilotOperationFingerprint.WorkspaceDraft(conversationId, "professional", "Changed instruction"));
        Assert.NotEqual(original, CopilotOperationFingerprint.WorkspaceDraft(Guid.NewGuid(), "professional", "Instruction"));
    }
}
