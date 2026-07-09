using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIResponseValidation;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed partial class AIResponseValidator(
    IAIContextRepository aiContextRepository,
    ICurrentTenantContext currentTenantContext,
    IOptions<AIPromptOptions> options) : IAIResponseValidator
{
    public AIResponseValidationResult Validate(AIResponseValidationRequest request)
    {
        var violations = new List<AIResponseViolationCode>();
        var response = request.ModelResponse ?? string.Empty;

        if (string.IsNullOrWhiteSpace(response))
        {
            violations.Add(AIResponseViolationCode.EmptyResponse);
        }

        if (response.Length > options.Value.MaxResponseCharacters)
        {
            violations.Add(AIResponseViolationCode.ResponseTooLong);
        }

        if (request.AIContext.Safety.RequiresPropertyAccessAuthorization && ContainsPropertyAccessCredential(response))
        {
            violations.Add(AIResponseViolationCode.PropertyAccessDisclosure);
        }

        if (ContainsProtectedIdentifier(response, request.ProtectedIdentifiers))
        {
            violations.Add(AIResponseViolationCode.InternalIdentifierDisclosure);
        }

        if (ContainsInternalNotesDisclosure(response))
        {
            violations.Add(AIResponseViolationCode.InternalNotesDisclosure);
        }

        if (ContainsUnsupportedApprovalClaim(response))
        {
            violations.Add(AIResponseViolationCode.UnsupportedApprovalClaim);
        }

        if (ContainsUnsupportedCompletionClaim(response))
        {
            violations.Add(AIResponseViolationCode.UnsupportedCompletionClaim);
        }

        if (ContainsPromptLeakage(response))
        {
            violations.Add(AIResponseViolationCode.PotentialPromptLeakage);
        }

        var result = BuildResult(violations);
        Audit(request, result);
        return result;
    }

    private static AIResponseValidationResult BuildResult(IReadOnlyCollection<AIResponseViolationCode> violations)
    {
        if (violations.Count == 0)
        {
            return new AIResponseValidationResult
            {
                Outcome = AIResponseValidationOutcome.Valid
            };
        }

        if (violations.Contains(AIResponseViolationCode.ResponseTooLong))
        {
            return new AIResponseValidationResult
            {
                Outcome = AIResponseValidationOutcome.EscalationRequired,
                Violations = violations,
                SafeMessage = AIResponseSafeMessages.ResponseUnavailable
            };
        }

        if (violations.Contains(AIResponseViolationCode.UnsupportedApprovalClaim)
            || violations.Contains(AIResponseViolationCode.UnsupportedCompletionClaim))
        {
            return new AIResponseValidationResult
            {
                Outcome = AIResponseValidationOutcome.Blocked,
                Violations = violations,
                SafeMessage = AIResponseSafeMessages.OperationalApprovalCannotBeConfirmed
            };
        }

        if (violations.Contains(AIResponseViolationCode.PropertyAccessDisclosure))
        {
            return new AIResponseValidationResult
            {
                Outcome = AIResponseValidationOutcome.Blocked,
                Violations = violations,
                SafeMessage = AIResponseSafeMessages.PropertyAccessVerificationRequired
            };
        }

        return new AIResponseValidationResult
        {
            Outcome = AIResponseValidationOutcome.Blocked,
            Violations = violations,
            SafeMessage = AIResponseSafeMessages.GeneralValidationFailure
        };
    }

    private void Audit(AIResponseValidationRequest request, AIResponseValidationResult result)
    {
        aiContextRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = "AIResponseValidation",
            EntityId = request.ProtectedIdentifiers.ReservationId ?? Guid.Empty,
            Action = "Validated",
            Details = JsonSerializer.Serialize(new
            {
                currentTenantContext.CorrelationId,
                ValidationOutcome = result.Outcome.ToString(),
                ViolationCodes = result.Violations.Select(violation => violation.ToString()).ToList(),
                QuestionCategories = request.QuestionCategories.Select(category => category.ToString()).ToList(),
                PropertyAccessRestricted = request.AIContext.Safety.RequiresPropertyAccessAuthorization
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, CancellationToken.None).GetAwaiter().GetResult();
        aiContextRepository.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private static bool ContainsPropertyAccessCredential(string response)
    {
        return AccessCredentialRegex().IsMatch(response);
    }

    private static bool ContainsProtectedIdentifier(string response, AIProtectedIdentifiers identifiers)
    {
        return identifiers.Values().Any(identifier => response.Contains(identifier.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsInternalNotesDisclosure(string response)
    {
        return ContainsAny(response, ["internal notes", "staff notes", "host-only note", "host only note", "private staff note"]);
    }

    private static bool ContainsUnsupportedApprovalClaim(string response)
    {
        return UnsupportedApprovalRegex().IsMatch(response);
    }

    private static bool ContainsUnsupportedCompletionClaim(string response)
    {
        return UnsupportedCompletionRegex().IsMatch(response);
    }

    private static bool ContainsPromptLeakage(string response)
    {
        return ContainsAny(response, ["system instructions", "developer message", "hidden prompt", "my system prompt says", "the instructions i was given"]);
    }

    private static bool ContainsAny(string response, IReadOnlyCollection<string> indicators)
    {
        return indicators.Any(indicator => response.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"\b(?:door|gate|lockbox|alarm|access|keypad)\s+(?:code|pin|password|credential)s?\b.{0,40}\b(?:is|are|:|-)?\s*[A-Za-z0-9]{3,12}\b|\b(?:pin|keypad code|access code)\b.{0,20}\b(?:is|:|-)\s*[A-Za-z0-9]{3,12}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AccessCredentialRegex();

    [GeneratedRegex(@"\b(?:late checkout|late check-out|reservation extension|extension|refund|cancellation refund|service upgrade)\b.{0,60}\b(?:approved|confirmed|granted|authorized)\b|\b(?:approved|confirmed|granted|authorized)\b.{0,60}\b(?:late checkout|late check-out|reservation extension|extension|refund|cancellation refund|service upgrade)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedApprovalRegex();

    [GeneratedRegex(@"\b(?:airport transfer|maintenance|laundry request|cleaning request|driver|chef|tour|service request)\b.{0,60}\b(?:booked|confirmed|completed|fixed|scheduled)\b|\b(?:booked|confirmed|completed|fixed|scheduled)\b.{0,60}\b(?:airport transfer|maintenance|laundry request|cleaning request|driver|chef|tour|service request)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedCompletionRegex();
}
