using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class DevelopmentWhatsAppCloudClientTests
{
    [Fact]
    public async Task SendTextMessageAsync_ReturnsDeterministicExternalIdAndStoresSanitizedRecord()
    {
        var store = new WhatsAppDevelopmentMessageStore();
        var client = new DevelopmentWhatsAppCloudClient(store, new PhoneNumberNormalizer());

        var first = await client.SendTextMessageAsync(new WhatsAppSendTextMessageRequest
        {
            PhoneNumberId = "demo-phone-number-id",
            To = "+14155551234",
            Body = "Welcome to StayFlow",
            ClientMessageId = "abc123",
            Origin = WhatsAppSendOrigin.ManualHost
        }, CancellationToken.None);
        var second = await client.SendTextMessageAsync(new WhatsAppSendTextMessageRequest
        {
            PhoneNumberId = "demo-phone-number-id",
            To = "+14155551234",
            Body = "Welcome to StayFlow",
            ClientMessageId = "abc123",
            Origin = WhatsAppSendOrigin.ManualHost
        }, CancellationToken.None);

        Assert.True(first.Success);
        Assert.Equal(first.ExternalMessageId, second.ExternalMessageId);

        var records = store.GetRecords().ToList();
        Assert.Equal(2, records.Count);
        Assert.All(records, record =>
        {
            Assert.Equal("+1******1234", record.ToMasked);
            Assert.Equal("abc123", record.ClientMessageId);
            Assert.DoesNotContain("+14155551234", record.ToMasked);
        });
    }
}