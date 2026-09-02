namespace StayFlow.Api.Models;

/// <summary>
/// Tenant-configured mapping from a lifecycle event type to an actual synced, Meta-approved
/// WhatsApp template. Used only when the customer-service window is closed, so proactive
/// lifecycle messages never fall back to free-form text.
/// </summary>
public sealed class ReservationLifecycleWhatsAppTemplateMapping : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid WhatsAppIntegrationId { get; set; }
    public Guid WhatsAppTemplateId { get; set; }
    public ReservationLifecycleEventType JourneyEventType { get; set; }

    // Empty string means "fallback for any guest language"; a specific value (e.g. "en") is tried
    // first when it matches the guest's preferred language. This lets one enabled mapping per
    // language coexist without ambiguity while keeping (CompanyId, IntegrationId, EventType,
    // LanguageCode) uniquely resolvable.
    public string LanguageCode { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // Ordered, comma-separated ReservationLifecycleTemplateParameter names bound to the template's
    // positional variables. Empty means the template takes no variables.
    public string ParameterBindings { get; set; } = string.Empty;

    public Company Company { get; set; } = null!;
    public WhatsAppIntegration WhatsAppIntegration { get; set; } = null!;
    public WhatsAppTemplate WhatsAppTemplate { get; set; } = null!;
}
