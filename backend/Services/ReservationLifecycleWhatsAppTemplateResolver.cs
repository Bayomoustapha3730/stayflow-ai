using System.Globalization;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed record ReservationLifecycleTemplateResolution(
    bool Resolved,
    WhatsAppTemplate? Template,
    IReadOnlyList<string> Variables,
    string? BlockedReason)
{
    public static ReservationLifecycleTemplateResolution Blocked(string reason) => new(false, null, [], reason);
}

/// <summary>
/// Resolves a tenant-configured, approved WhatsApp template for a lifecycle event type. Never
/// selects an arbitrary approved template: resolution fails closed (Blocked) unless an explicit,
/// enabled mapping exists and the mapped template is still valid to send.
/// </summary>
public interface IReservationLifecycleWhatsAppTemplateResolver
{
    Task<ReservationLifecycleTemplateResolution> ResolveAsync(
        Guid companyId,
        Guid integrationId,
        ReservationLifecycleEventType eventType,
        string? guestPreferredLanguage,
        Reservation reservation,
        Property property,
        Guest guest,
        CancellationToken cancellationToken);
}

public sealed class ReservationLifecycleWhatsAppTemplateResolver(
    IReservationLifecycleWhatsAppTemplateMappingRepository mappingRepository) : IReservationLifecycleWhatsAppTemplateResolver
{
    public async Task<ReservationLifecycleTemplateResolution> ResolveAsync(
        Guid companyId,
        Guid integrationId,
        ReservationLifecycleEventType eventType,
        string? guestPreferredLanguage,
        Reservation reservation,
        Property property,
        Guest guest,
        CancellationToken cancellationToken)
    {
        var normalizedPreferred = guestPreferredLanguage?.Trim().ToLowerInvariant();
        var candidateLanguages = string.IsNullOrEmpty(normalizedPreferred)
            ? new[] { string.Empty }
            : new[] { normalizedPreferred, string.Empty };

        ReservationLifecycleWhatsAppTemplateMapping? mapping = null;
        foreach (var candidate in candidateLanguages)
        {
            mapping = await mappingRepository.GetEnabledMappingAsync(companyId, integrationId, eventType, candidate, cancellationToken);
            if (mapping is not null)
            {
                break;
            }
        }

        if (mapping is null)
        {
            return ReservationLifecycleTemplateResolution.Blocked($"No enabled WhatsApp template mapping is configured for {eventType}.");
        }

        var template = mapping.WhatsAppTemplate;
        if (template is null || template.CompanyId != companyId || template.WhatsAppIntegrationId != integrationId)
        {
            return ReservationLifecycleTemplateResolution.Blocked("Configured lifecycle template does not belong to this company/integration.");
        }

        if (!template.IsActive || !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return ReservationLifecycleTemplateResolution.Blocked("Configured lifecycle template is not approved for sending.");
        }

        if (!TryBindParameters(mapping.ParameterBindings, reservation, property, guest, out var variables, out var bindingError))
        {
            return ReservationLifecycleTemplateResolution.Blocked(bindingError!);
        }

        if (variables.Count != Math.Max(0, template.VariableCount))
        {
            return ReservationLifecycleTemplateResolution.Blocked("Configured lifecycle template parameter bindings do not match the template's variable count.");
        }

        return new ReservationLifecycleTemplateResolution(true, template, variables, null);
    }

    private static bool TryBindParameters(
        string parameterBindings,
        Reservation reservation,
        Property property,
        Guest guest,
        out IReadOnlyList<string> variables,
        out string? error)
    {
        variables = [];
        error = null;

        var tokens = parameterBindings
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var bound = new List<string>(tokens.Length);
        foreach (var token in tokens)
        {
            if (!Enum.TryParse<ReservationLifecycleTemplateParameter>(token, ignoreCase: true, out var parameter))
            {
                error = $"Configured lifecycle template parameter binding '{token}' is not a recognized parameter.";
                return false;
            }

            bound.Add(parameter switch
            {
                ReservationLifecycleTemplateParameter.GuestFirstName => guest.FirstName.Trim(),
                ReservationLifecycleTemplateParameter.PropertyName => property.Name.Trim(),
                ReservationLifecycleTemplateParameter.CheckInDate => reservation.CheckInDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture),
                ReservationLifecycleTemplateParameter.CheckOutDate => reservation.CheckOutDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture),
                _ => string.Empty
            });
        }

        variables = bound;
        return true;
    }
}
