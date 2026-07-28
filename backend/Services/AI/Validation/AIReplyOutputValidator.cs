using System.Text;
using System.Text.RegularExpressions;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Services.AI.Validation;

public sealed class AIReplyOutputValidator : IAIReplyOutputValidator
{
    public AIReplyValidationResult Validate(
        AIReplyOperation operation,
        string? output,
        IReadOnlyCollection<string> suggestions,
        int maxOutputCharacters,
        int expectedSuggestionCount,
        bool contextIncomplete)
    {
        var errors = new List<string>();
        var warnings = new List<AIReplyOrchestrationWarning>();

        var normalizedOutput = NormalizeText(output);
        var normalizedSuggestions = suggestions
            .Select(NormalizeText)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        if (operation == AIReplyOperation.SuggestedHostReplies)
        {
            if (normalizedSuggestions.Count != expectedSuggestionCount)
            {
                errors.Add($"Expected exactly {expectedSuggestionCount} suggestions.");
            }

            if (normalizedSuggestions
                .Select(NormalizationKey)
                .Distinct(StringComparer.Ordinal)
                .Count() != normalizedSuggestions.Count)
            {
                errors.Add("Duplicate suggestions were generated after normalization.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(normalizedOutput))
            {
                errors.Add("Reply output is blank.");
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedOutput) && normalizedOutput.Length > maxOutputCharacters)
        {
            errors.Add($"Reply output exceeded the maximum length of {maxOutputCharacters} characters.");
        }

        foreach (var item in normalizedSuggestions)
        {
            if (item.Length > maxOutputCharacters)
            {
                errors.Add($"A suggestion exceeded the maximum length of {maxOutputCharacters} characters.");
            }
        }

        var leakSource = string.Join("\n", normalizedSuggestions.Append(normalizedOutput ?? string.Empty));

        if (ContainsPromptLeakage(leakSource))
        {
            errors.Add("Prompt or system-instruction leakage was detected.");
        }

        if (ContainsInternalNoteDisclosure(leakSource))
        {
            errors.Add("Internal note disclosure was detected.");
        }

        if (ContainsUnsafeHtml(leakSource))
        {
            errors.Add("Unsafe HTML markup was detected.");
        }

        if (ContainsRawIds(leakSource))
        {
            errors.Add("Raw internal identifier leakage was detected.");
        }

        if (contextIncomplete && ContainsOverconfidentLanguage(leakSource))
        {
            warnings.Add(new AIReplyOrchestrationWarning(
                "OverconfidentLanguage",
                "The reply uses certainty language while context is incomplete.",
                "info"));
        }

        return new AIReplyValidationResult(
            errors.Count == 0,
            normalizedOutput,
            normalizedSuggestions,
            errors,
            warnings);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedNewLines = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var noControl = new string(normalizedNewLines.Where(ch => ch == '\n' || ch == '\t' || !char.IsControl(ch)).ToArray());
        var trimmed = noControl.Trim().Trim('"', '\'', '`', ',', ';', ':');

        var compactedLines = trimmed
            .Split('\n')
            .Select(line => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToList();

        var builder = new StringBuilder();
        var previousBlank = false;
        foreach (var line in compactedLines)
        {
            var isBlank = string.IsNullOrWhiteSpace(line);
            if (isBlank && previousBlank)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
            previousBlank = isBlank;
        }

        return builder.ToString().Trim();
    }

    private static string NormalizationKey(string value)
    {
        return string.Join(' ', value
            .ToLowerInvariant()
            .Split(['\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool ContainsPromptLeakage(string value)
    {
        return ContainsAny(value, ["system instruction", "developer instruction", "hidden prompt", "prompt says", "implementation detail"]);
    }

    private static bool ContainsInternalNoteDisclosure(string value)
    {
        return ContainsAny(value, ["internal note", "staff note", "host-only note", "private note"]);
    }

    private static bool ContainsUnsafeHtml(string value)
    {
        return Regex.IsMatch(value, "<\\s*(script|iframe|object|embed|style|link|meta|form|input|button)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
               || Regex.IsMatch(value, "<[^>]+>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsRawIds(string value)
    {
        return Regex.IsMatch(value, "\\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\\b", RegexOptions.CultureInvariant);
    }

    private static bool ContainsOverconfidentLanguage(string value)
    {
        return ContainsAny(value, ["definitely", "guaranteed", "certainly", "confirmed", "for sure", "always"]);
    }

    private static bool ContainsAny(string value, IReadOnlyCollection<string> markers)
    {
        return markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
