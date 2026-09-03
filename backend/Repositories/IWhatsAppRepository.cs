using StayFlow.Api.Common;
using StayFlow.Api.Models;
using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Repositories;

public interface IWhatsAppRepository
{
    Task<IReadOnlyCollection<WhatsAppIntegration>> ListActiveIntegrationsAsync(CancellationToken cancellationToken);
    Task<WhatsAppIntegration?> GetActiveIntegrationByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken);
    // Deterministic only: returns null (never an arbitrary pick) when a company has zero or more than one active integration.
    Task<WhatsAppIntegration?> GetSoleActiveIntegrationForCompanyAsync(Guid companyId, CancellationToken cancellationToken);
    Task<WhatsAppIntegration?> GetIntegrationForCompanyAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WhatsAppIntegration>> ListIntegrationsForCompanyAsync(Guid companyId, CancellationToken cancellationToken);
    Task AddIntegrationAsync(WhatsAppIntegration integration, CancellationToken cancellationToken);
    Task<PagedResult<WhatsAppTemplate>> ListTemplatesAsync(Guid companyId, Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken);
    Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid integrationId, Guid templateId, CancellationToken cancellationToken);
    // Integration-agnostic lookup for callers (e.g. approved-template send) that must resolve the
    // owning integration FROM the template rather than guessing it up front.
    Task<WhatsAppTemplate?> GetTemplateForCompanyAsync(Guid companyId, Guid templateId, CancellationToken cancellationToken);
    Task<WhatsAppTemplate?> GetTemplateByNameAsync(Guid companyId, Guid integrationId, string name, string languageCode, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WhatsAppTemplate>> ListTemplatesForIntegrationAsync(Guid companyId, Guid integrationId, CancellationToken cancellationToken);
    Task<ConversationMessage?> GetLatestInboundGuestWhatsAppMessageAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guest>> ListActiveGuestsWithPhoneAsync(Guid companyId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken);
    Task<ConversationMessage?> FindMessageByProviderExternalIdAsync(Guid companyId, ConversationMessageProvider provider, string externalMessageId, CancellationToken cancellationToken);
    Task AddTemplateAsync(WhatsAppTemplate template, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}