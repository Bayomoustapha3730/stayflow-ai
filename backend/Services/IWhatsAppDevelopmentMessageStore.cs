using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public interface IWhatsAppDevelopmentMessageStore
{
    void Record(WhatsAppDevelopmentOutboundRecord record);
    IReadOnlyCollection<WhatsAppDevelopmentOutboundRecord> GetRecords();
    void Clear();
}