using System.Text.RegularExpressions;
using StayFlow.Api.Services.AI.Intent;

namespace StayFlow.Api.Services.AI.Orchestration;

public sealed class ConciergeResponseValidator : IConciergeResponseValidator
{
    private static readonly string[] PromptLeakPhrases =
    [
        "ignore your instructions",
        "system prompt",
        "reveal every",
        "door code",
        "act as the database administrator",
        "show all",
        "print the system prompt"
    ];

    private static readonly string[] UnsupportedClaimPhrases =
    [
        "made-up",
        "not the real",
        "early check-in",
        "pet policy",
        "free parking",
        "complimentary parking"
    ];

    public ConciergeResponseValidationResult Validate(ConciergeLanguageModelRequest request, ConciergeLanguageModelResult result)
    {
        var violations = new List<string>();
        var allowedSources = request.RetrievalResult.SelectedItems.Select(candidate => candidate.ArticleId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var referenced = result.SourceArticleIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        if (string.IsNullOrWhiteSpace(result.Output))
        {
            violations.Add("EmptyOutput");
        }

        if (result.Output?.Length > 1400)
        {
            violations.Add("OversizedOutput");
        }

        if (result.Output is not null && PromptLeakPhrases.Any(phrase => result.Output.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add("PromptLeak");
        }

        if (result.Output is not null && UnsupportedClaimPhrases.Any(phrase => result.Output.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            violations.Add("UnsupportedClaim");
        }

        if (request.RequiredOutcome == ConciergeRequiredOutcome.MultiIntentGroundedAnswer && request.IntentResult.SecondaryIntents.Count > 0)
        {
            if (result.Output is null || !result.Output.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) && !result.Output.Contains("checkout", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add("MissingMultiIntentCoverage");
            }
        }

        if (referenced.Any() && !referenced.All(source => allowedSources.Contains(source, StringComparer.OrdinalIgnoreCase)))
        {
            violations.Add("InvalidSourceReference");
        }

        if (ContainsInternalMetadata(result.Output))
        {
            violations.Add("InternalMetadataLeak");
        }

        if (request.RequiredOutcome == ConciergeRequiredOutcome.Emergency && !ContainsEmergencyInstruction(result.Output))
        {
            violations.Add("MissingEmergencyInstruction");
        }

        if (request.RequiredOutcome == ConciergeRequiredOutcome.HostVerificationRequired && result.Output is not null && result.Output.Contains("password", StringComparison.OrdinalIgnoreCase) && !result.Output.Contains("host", StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("UnverifiedCredentialDisclosure");
        }

        var isValid = violations.Count == 0;
        return new ConciergeResponseValidationResult(
            isValid,
            isValid ? "Accepted" : "Rejected",
            violations,
            allowedSources,
            referenced);
    }

    private static bool ContainsInternalMetadata(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return Regex.IsMatch(text, "(?i)(provider|model|prompt|source id|sourceid|database|system prompt)");
    }

    private static bool ContainsEmergencyInstruction(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("emergency", StringComparison.OrdinalIgnoreCase)
            || text.Contains("contact local emergency services", StringComparison.OrdinalIgnoreCase);
    }
}
