using System.Security.Cryptography;
using System.Text;
using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public sealed class DevelopmentWhatsAppCloudClient(
    IWhatsAppDevelopmentMessageStore messageStore,
    IPhoneNumberNormalizer phoneNumberNormalizer) : IWhatsAppCloudClient
{
    public Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken)
    {
        var externalMessageId = CreateDeterministicExternalId(request.ClientMessageId, request.To, request.Body);
        messageStore.Record(new WhatsAppDevelopmentOutboundRecord
        {
            PhoneNumberId = request.PhoneNumberId,
            ToMasked = phoneNumberNormalizer.Mask(request.To) ?? "unknown",
            BodyPreview = CreateBodyPreview(request.Body),
            ClientMessageId = request.ClientMessageId,
            ExternalMessageId = externalMessageId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return Task.FromResult(new WhatsAppSendTextMessageResult
        {
            Success = true,
            ExternalMessageId = externalMessageId
        });
    }

    private static string CreateDeterministicExternalId(string clientMessageId, string to, string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{clientMessageId}|{to}|{body}"));
        return $"dev-wa-{Convert.ToHexString(bytes[..8]).ToLowerInvariant()}";
    }

    private static string CreateBodyPreview(string body)
    {
        var trimmed = body.Trim();
        return trimmed.Length <= 32 ? trimmed : $"{trimmed[..32]}...";
    }
}