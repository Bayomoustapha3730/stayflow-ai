using System.Text.RegularExpressions;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Grounding;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services.AI.Safety;

public sealed class AIReplySafetyEvaluator : IAIReplySafetyEvaluator
{
    public AIReplySafetyResult Evaluate(
        AIReplyOperation operation,
        string? output,
        IReadOnlyCollection<string> suggestions,
        ConversationContext context,
        IReadOnlyCollection<ConversationContextKnowledgeItem> selectedKnowledgeItems,
        GuestIntentResult? detectedIntent,
        int contextConfidence,
        bool fallbackUsed)
    {
        var warnings = new List<AIReplyOrchestrationWarning>();
        var blockedReasons = new List<string>();
        var combined = string.Join("\n", suggestions.Append(output ?? string.Empty));

        if (Contains(combined, "refund") && ContainsAny(combined, ["approved", "confirmed", "granted"]))
        {
            warnings.Add(new AIReplyOrchestrationWarning("FabricatedRefund", "Possible unsupported refund approval claim detected."));
            blockedReasons.Add("Fabricated refund claim.");
        }

        if (ContainsAny(combined, ["approved", "confirmed", "granted"]) && ContainsAny(combined, ["late checkout", "reservation extension", "booking change"]))
        {
            warnings.Add(new AIReplyOrchestrationWarning("FabricatedApproval", "Possible unsupported approval claim detected."));
            blockedReasons.Add("Fabricated approval claim.");
        }

        var selected = selectedKnowledgeItems
            .Select(item => new AIProviderKnowledgeItem
            {
                SourceId = item.SourceId,
                Title = item.Title,
                Category = item.Category.ToString(),
                Tags = item.Tags,
                Summary = item.Summary,
                Content = item.Content,
                Priority = item.Priority,
                IsApproved = item.IsApproved
            })
            .ToList();

        var wifiGrounding = DeterministicGrounding.ExtractWiFi(selected);
        var containsAccessCodeValue = ContainsAny(combined, ["door code", "lockbox code", "access code", "alarm code", "pin code"])
            && Regex.IsMatch(combined, "\\b[A-Za-z0-9]{3,24}\\b", RegexOptions.CultureInvariant);

        if (containsAccessCodeValue)
        {
            warnings.Add(new AIReplyOrchestrationWarning("FabricatedPassword", "Possible access credential disclosure detected."));
            blockedReasons.Add("Possible credential disclosure.");
        }

        var includesPasswordDisclosure = Regex.IsMatch(
            combined,
            "\\b(?:password|passcode)\\b\\s*(?:is|:|=)\\s*(?<value>[A-Za-z0-9!@#$%^&*()_+\\-={}[\\]|:;,.?/]{3,64})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (includesPasswordDisclosure)
        {
            var supportedPassword = wifiGrounding.DistinctPasswords.Count == 1
                ? wifiGrounding.DistinctPasswords.FirstOrDefault()
                : null;
            var containsSupportedPassword = !string.IsNullOrWhiteSpace(supportedPassword)
                && combined.Contains(supportedPassword, StringComparison.OrdinalIgnoreCase);
            var wifiIntent = detectedIntent?.Intent == GuestIntent.WiFi;

            if (!wifiIntent || wifiGrounding.HasConflict || string.IsNullOrWhiteSpace(supportedPassword) || !containsSupportedPassword)
            {
                warnings.Add(new AIReplyOrchestrationWarning("FabricatedPassword", "Unsupported password disclosure detected."));
                blockedReasons.Add("Unsupported credential disclosure.");
            }
        }

        if (ContainsAny(combined, ["system prompt", "developer instruction", "hidden prompt"]))
        {
            warnings.Add(new AIReplyOrchestrationWarning("PromptDisclosure", "Prompt disclosure language detected."));
            blockedReasons.Add("Prompt disclosure language.");
        }

        if (ContainsAny(combined, ["internal note", "staff note", "private note"]))
        {
            warnings.Add(new AIReplyOrchestrationWarning("InternalNoteDisclosure", "Internal note disclosure language detected."));
            blockedReasons.Add("Internal note disclosure.");
        }

        if (ContainsAny(combined, ["do not call emergency services", "ignore emergency services"]))
        {
            warnings.Add(new AIReplyOrchestrationWarning("UnsafeEmergencyGuidance", "Unsafe emergency guidance detected."));
            blockedReasons.Add("Unsafe emergency guidance.");
        }

        var requiresHumanReview = fallbackUsed || contextConfidence < 60;
        if (contextConfidence < 50 && ContainsAny(combined, ["confirmed", "definitely", "certainly"]))
        {
            warnings.Add(new AIReplyOrchestrationWarning("LowConfidenceCertainty", "Reply confidence is low and certainty language was detected."));
            requiresHumanReview = true;
        }

        if (operation == AIReplyOperation.FutureGuestReply && warnings.Count > 0)
        {
            blockedReasons.Add("Future guest reply operation cannot dispatch unsafe output.");
        }

        var safe = blockedReasons.Count == 0;
        return new AIReplySafetyResult(safe, warnings, requiresHumanReview, blockedReasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool Contains(string text, string marker)
    {
        return text.Contains(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string text, IReadOnlyCollection<string> markers)
    {
        return markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
