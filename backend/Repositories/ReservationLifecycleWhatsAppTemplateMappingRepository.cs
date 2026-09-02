using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public sealed class ReservationLifecycleWhatsAppTemplateMappingRepository(ApplicationDbContext dbContext) : IReservationLifecycleWhatsAppTemplateMappingRepository
{
    public Task<ReservationLifecycleWhatsAppTemplateMapping?> GetEnabledMappingAsync(
        Guid companyId,
        Guid integrationId,
        ReservationLifecycleEventType eventType,
        string languageCode,
        CancellationToken cancellationToken)
    {
        return dbContext.ReservationLifecycleWhatsAppTemplateMappings
            .Include(item => item.WhatsAppTemplate)
            .FirstOrDefaultAsync(item => item.CompanyId == companyId
                && item.WhatsAppIntegrationId == integrationId
                && item.JourneyEventType == eventType
                && item.LanguageCode == languageCode
                && item.IsEnabled,
                cancellationToken);
    }
}
