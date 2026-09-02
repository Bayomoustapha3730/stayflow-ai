using StayFlow.Api.Models;

namespace StayFlow.Api.Repositories;

public interface IReservationLifecycleWhatsAppTemplateMappingRepository
{
    Task<ReservationLifecycleWhatsAppTemplateMapping?> GetEnabledMappingAsync(
        Guid companyId,
        Guid integrationId,
        ReservationLifecycleEventType eventType,
        string languageCode,
        CancellationToken cancellationToken);
}
