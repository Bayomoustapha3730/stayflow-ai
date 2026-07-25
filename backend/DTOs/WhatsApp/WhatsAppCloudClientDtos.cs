namespace StayFlow.Api.DTOs.WhatsApp;

public sealed class WhatsAppSendTextMessageRequest
{
    public string PhoneNumberId { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string ClientMessageId { get; init; } = string.Empty;
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