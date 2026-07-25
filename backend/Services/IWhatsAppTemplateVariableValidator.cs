using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class WhatsAppTemplateVariableValidationResult
{
    public bool Success { get; init; }
    public IReadOnlyCollection<string> Errors { get; init; } = [];
    public IReadOnlyCollection<string> SanitizedVariables { get; init; } = [];
}

public interface IWhatsAppTemplateVariableValidator
{
    WhatsAppTemplateVariableValidationResult Validate(WhatsAppTemplate template, IReadOnlyCollection<string> variables);
}
