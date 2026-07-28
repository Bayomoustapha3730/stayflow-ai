using System.Text.Json;
using System.Text.Json.Serialization;

namespace StayFlow.Api.DTOs.WhatsApp;

public sealed class WhatsAppWebhookPayload
{
    [JsonPropertyName("object")]
    public string? Object { get; init; }

    [JsonPropertyName("entry")]
    public IReadOnlyCollection<WhatsAppWebhookEntry> Entry { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("changes")]
    public IReadOnlyCollection<WhatsAppWebhookChange> Changes { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookChange
{
    [JsonPropertyName("field")]
    public string? Field { get; init; }

    [JsonPropertyName("value")]
    public WhatsAppWebhookValue? Value { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookValue
{
    [JsonPropertyName("metadata")]
    public WhatsAppWebhookMetadata? Metadata { get; init; }

    [JsonPropertyName("contacts")]
    public IReadOnlyCollection<WhatsAppWebhookContact> Contacts { get; init; } = [];

    [JsonPropertyName("messages")]
    public IReadOnlyCollection<WhatsAppWebhookMessage> Messages { get; init; } = [];

    [JsonPropertyName("statuses")]
    public IReadOnlyCollection<WhatsAppWebhookStatus> Statuses { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookMetadata
{
    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookContact
{
    [JsonPropertyName("wa_id")]
    public string? WhatsAppId { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookMessage
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("text")]
    public WhatsAppWebhookText? Text { get; init; }

    [JsonPropertyName("context")]
    public WhatsAppWebhookMessageContext? Context { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookText
{
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookMessageContext
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("recipient_id")]
    public string? RecipientId { get; init; }

    [JsonPropertyName("errors")]
    public IReadOnlyCollection<WhatsAppWebhookError> Errors { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed class WhatsAppWebhookError
{
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}