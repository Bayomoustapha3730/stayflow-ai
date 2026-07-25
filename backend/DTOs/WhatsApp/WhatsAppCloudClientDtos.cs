namespace StayFlow.Api.DTOs.WhatsApp;

public sealed class WhatsAppSendTextMessageRequest
{
    public string AccessToken { get; init; } = string.Empty;
    public string GraphApiVersion { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string ClientMessageId { get; init; } = string.Empty;
}

public sealed class WhatsAppTemplateSendRequest
{
    public string AccessToken { get; init; } = string.Empty;
    public string GraphApiVersion { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public IReadOnlyCollection<string> Variables { get; init; } = [];
    public string ClientMessageId { get; init; } = string.Empty;
}

public sealed class WhatsAppProviderTemplate
{
    public string ExternalTemplateId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? HeaderType { get; init; }
    public string BodyText { get; init; } = string.Empty;
    public string? FooterText { get; init; }
    public IReadOnlyCollection<string> Placeholders { get; init; } = [];
    public string ComponentsJson { get; init; } = "[]";
}

public sealed class WhatsAppGetTemplatesResult
{
    public bool Success { get; init; }
    public bool IsTransientFailure { get; init; }
    public IReadOnlyCollection<WhatsAppProviderTemplate> Templates { get; init; } = [];
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
}

public sealed class WhatsAppGetTemplatesRequest
{
    public string AccessToken { get; init; } = string.Empty;
    public string GraphApiVersion { get; init; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; init; } = string.Empty;
}

public sealed class WhatsAppValidateIntegrationRequest
{
    public string AccessToken { get; init; } = string.Empty;
    public string GraphApiVersion { get; init; } = string.Empty;
    public string WhatsAppBusinessAccountId { get; init; } = string.Empty;
}

public sealed class WhatsAppSendTemplateMessageResult
{
    public bool Success { get; init; }
    public bool IsTransientFailure { get; init; }
    public string? ExternalMessageId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
}

public sealed class WhatsAppValidateIntegrationResult
{
    public bool Success { get; init; }
    public bool IsTransientFailure { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
}

public sealed class WhatsAppSendTextMessageResult
{
    public bool Success { get; init; }
    public bool IsTransientFailure { get; init; }
    public string? ExternalMessageId { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureReason { get; init; }
}

public sealed class WhatsAppDevelopmentOutboundRecord
{
    public string PhoneNumberId { get; init; } = string.Empty;
    public string ToMasked { get; init; } = string.Empty;
    public string BodyPreview { get; init; } = string.Empty;
    public string ClientMessageId { get; init; } = string.Empty;
    public string ExternalMessageId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}