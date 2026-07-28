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

    public Task<WhatsAppGetTemplatesResult> GetTemplatesAsync(WhatsAppGetTemplatesRequest request, CancellationToken cancellationToken)
    {
        var templates = new List<WhatsAppProviderTemplate>
        {
            new()
            {
                ExternalTemplateId = "dev-template-001",
                Name = "welcome_back",
                LanguageCode = "en_US",
                Category = "UTILITY",
                Status = "APPROVED",
                HeaderType = "TEXT",
                BodyText = "Hello {{1}}, your booking {{2}} is confirmed.",
                FooterText = "StayFlow",
                Placeholders = ["{{1}}", "{{2}}"],
                ComponentsJson = "[]"
            },
            new()
            {
                ExternalTemplateId = "dev-template-002",
                Name = "checkin_reminder",
                LanguageCode = "en_US",
                Category = "UTILITY",
                Status = "PENDING",
                BodyText = "Reminder: check-in starts at {{1}}.",
                Placeholders = ["{{1}}"],
                ComponentsJson = "[]"
            },
            new()
            {
                ExternalTemplateId = "dev-template-003",
                Name = "promo_offer",
                LanguageCode = "en_US",
                Category = "MARKETING",
                Status = "REJECTED",
                BodyText = "Hi {{1}}, enjoy our latest offer.",
                Placeholders = ["{{1}}"],
                ComponentsJson = "[]"
            }
        };

        return Task.FromResult(new WhatsAppGetTemplatesResult
        {
            Success = true,
            Templates = templates
        });
    }

    public Task<WhatsAppSendTemplateMessageResult> SendTemplateMessageAsync(WhatsAppTemplateSendRequest request, CancellationToken cancellationToken)
    {
        var externalMessageId = CreateDeterministicExternalId(request.ClientMessageId, request.To, $"{request.TemplateName}|{request.LanguageCode}|{string.Join("|", request.Variables)}");
        return Task.FromResult(new WhatsAppSendTemplateMessageResult
        {
            Success = true,
            ExternalMessageId = externalMessageId
        });
    }

    public Task<WhatsAppValidateIntegrationResult> ValidateIntegrationAsync(WhatsAppValidateIntegrationRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new WhatsAppValidateIntegrationResult
        {
            Success = true
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