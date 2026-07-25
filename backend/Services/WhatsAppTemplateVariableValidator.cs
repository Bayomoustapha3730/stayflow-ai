using System.Text.RegularExpressions;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class WhatsAppTemplateVariableValidator : IWhatsAppTemplateVariableValidator
{
    private static readonly Regex ControlCharactersRegex = new("[\\u0000-\\u0008\\u000B\\u000C\\u000E-\\u001F]", RegexOptions.Compiled);

    public WhatsAppTemplateVariableValidationResult Validate(WhatsAppTemplate template, IReadOnlyCollection<string> variables)
    {
        var errors = new List<string>();
        var expectedCount = Math.Max(0, template.VariableCount);

        if (variables.Count != expectedCount)
        {
            errors.Add($"Template requires {expectedCount} variable value(s).");
        }

        var normalized = variables.Select(value => value?.Trim() ?? string.Empty).ToList();
        for (var index = 0; index < normalized.Count; index++)
        {
            var value = normalized[index];
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Variable {index + 1} is required.");
                continue;
            }

            if (value.Length > 512)
            {
                errors.Add($"Variable {index + 1} exceeds the maximum length.");
            }

            if (ControlCharactersRegex.IsMatch(value))
            {
                errors.Add($"Variable {index + 1} contains unsupported control characters.");
            }
        }

        return new WhatsAppTemplateVariableValidationResult
        {
            Success = errors.Count == 0,
            Errors = errors,
            SanitizedVariables = normalized
        };
    }
}
