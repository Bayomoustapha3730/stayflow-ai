using System.Collections.Concurrent;
using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public sealed class WhatsAppDevelopmentMessageStore : IWhatsAppDevelopmentMessageStore
{
    private readonly ConcurrentQueue<WhatsAppDevelopmentOutboundRecord> records = new();

    public void Record(WhatsAppDevelopmentOutboundRecord record)
    {
        records.Enqueue(record);
    }

    public IReadOnlyCollection<WhatsAppDevelopmentOutboundRecord> GetRecords()
    {
        return records.ToArray();
    }

    public void Clear()
    {
        while (records.TryDequeue(out _))
        {
        }
    }
}