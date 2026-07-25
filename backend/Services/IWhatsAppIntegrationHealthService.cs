using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IWhatsAppIntegrationHealthService
{
    Task<WhatsAppIntegrationHealthResponse> CheckAsync(WhatsAppIntegration integration, CancellationToken cancellationToken);
}
