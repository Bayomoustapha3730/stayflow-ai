using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.WhatsApp;

namespace StayFlow.Api.Services;

public sealed class WhatsAppCloudClient(
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppCloudOptions> options,
    ILogger<WhatsAppCloudClient> logger) : IWhatsAppCloudClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<WhatsAppSendTextMessageResult> SendTextMessageAsync(WhatsAppSendTextMessageRequest request, CancellationToken cancellationToken)
    {
        var currentOptions = options.Value;
        using var client = httpClientFactory.CreateClient(nameof(WhatsAppCloudClient));
        client.Timeout = TimeSpan.FromSeconds(currentOptions.RequestTimeoutSeconds);
        client.BaseAddress = new Uri(currentOptions.GraphApiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var endpoint = $"{request.GraphApiVersion.Trim('/')}/{request.PhoneNumberId}/messages";
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "text",
            text = new { body = request.Body }
        };

        using var response = await client.PostAsJsonAsync(endpoint, payload, SerializerOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<WhatsAppCloudSendResponse>(SerializerOptions, cancellationToken);
            return new WhatsAppSendTextMessageResult
            {
                Success = true,
                ExternalMessageId = body?.Messages?.FirstOrDefault()?.Id
            };
        }

        var failure = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "WhatsApp Cloud send failed. StatusCode={StatusCode} ClientMessageId={ClientMessageId}",
            (int)response.StatusCode,
            request.ClientMessageId);

        return new WhatsAppSendTextMessageResult
        {
            Success = false,
            IsTransientFailure = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout,
            FailureCode = ((int)response.StatusCode).ToString(),
            FailureReason = SanitizeFailure(failure)
        };
    }

    public async Task<WhatsAppGetTemplatesResult> GetTemplatesAsync(WhatsAppGetTemplatesRequest request, CancellationToken cancellationToken)
    {
        var currentOptions = options.Value;
        using var client = httpClientFactory.CreateClient(nameof(WhatsAppCloudClient));
        client.Timeout = TimeSpan.FromSeconds(currentOptions.RequestTimeoutSeconds);
        client.BaseAddress = new Uri(currentOptions.GraphApiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var endpoint = $"{request.GraphApiVersion.Trim('/')}/{request.WhatsAppBusinessAccountId}/message_templates";
        using var response = await client.GetAsync(endpoint, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<WhatsAppCloudTemplatesResponse>(SerializerOptions, cancellationToken);
            var templates = body?.Data?.Select(MapTemplate).ToList() ?? [];

            return new WhatsAppGetTemplatesResult
            {
                Success = true,
                Templates = templates
            };
        }

        var failure = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("WhatsApp template fetch failed. StatusCode={StatusCode}", (int)response.StatusCode);

        return new WhatsAppGetTemplatesResult
        {
            Success = false,
            IsTransientFailure = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout,
            FailureCode = ((int)response.StatusCode).ToString(),
            FailureReason = SanitizeFailure(failure)
        };
    }

    public async Task<WhatsAppSendTemplateMessageResult> SendTemplateMessageAsync(WhatsAppTemplateSendRequest request, CancellationToken cancellationToken)
    {
        var currentOptions = options.Value;
        using var client = httpClientFactory.CreateClient(nameof(WhatsAppCloudClient));
        client.Timeout = TimeSpan.FromSeconds(currentOptions.RequestTimeoutSeconds);
        client.BaseAddress = new Uri(currentOptions.GraphApiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", request.AccessToken);

        var endpoint = $"{request.GraphApiVersion.Trim('/')}/{request.PhoneNumberId}/messages";

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "template",
            template = new
            {
                name = request.TemplateName,
                language = new { code = request.LanguageCode },
                components = request.Variables.Count == 0
                    ? Array.Empty<object>()
                    : new object[]
                    {
                        new
                        {
                            type = "body",
                            parameters = request.Variables.Select(value => new { type = "text", text = value }).ToArray()
                        }
                    }
            }
        };

        using var response = await client.PostAsJsonAsync(endpoint, payload, SerializerOptions, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<WhatsAppCloudSendResponse>(SerializerOptions, cancellationToken);
            return new WhatsAppSendTemplateMessageResult
            {
                Success = true,
                ExternalMessageId = body?.Messages?.FirstOrDefault()?.Id
            };
        }

        var failure = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "WhatsApp Cloud template send failed. StatusCode={StatusCode} ClientMessageId={ClientMessageId}",
            (int)response.StatusCode,
            request.ClientMessageId);

        return new WhatsAppSendTemplateMessageResult
        {
            Success = false,
            IsTransientFailure = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout,
            FailureCode = ((int)response.StatusCode).ToString(),
            FailureReason = SanitizeFailure(failure)
        };
    }

    public async Task<WhatsAppValidateIntegrationResult> ValidateIntegrationAsync(WhatsAppValidateIntegrationRequest request, CancellationToken cancellationToken)
    {
        var result = await GetTemplatesAsync(new WhatsAppGetTemplatesRequest
        {
            AccessToken = request.AccessToken,
            GraphApiVersion = request.GraphApiVersion,
            WhatsAppBusinessAccountId = request.WhatsAppBusinessAccountId
        }, cancellationToken);

        return new WhatsAppValidateIntegrationResult
        {
            Success = result.Success,
            IsTransientFailure = result.IsTransientFailure,
            FailureCode = result.FailureCode,
            FailureReason = result.FailureReason
        };
    }

    private static WhatsAppProviderTemplate MapTemplate(WhatsAppCloudTemplate template)
    {
        var body = template.Components?.FirstOrDefault(component => string.Equals(component.Type, "BODY", StringComparison.OrdinalIgnoreCase));
        var header = template.Components?.FirstOrDefault(component => string.Equals(component.Type, "HEADER", StringComparison.OrdinalIgnoreCase));
        var footer = template.Components?.FirstOrDefault(component => string.Equals(component.Type, "FOOTER", StringComparison.OrdinalIgnoreCase));
        var bodyText = body?.Text ?? string.Empty;
        var placeholders = ExtractPlaceholders(bodyText);

        return new WhatsAppProviderTemplate
        {
            ExternalTemplateId = template.Id ?? string.Empty,
            Name = template.Name ?? string.Empty,
            LanguageCode = template.Language ?? string.Empty,
            Category = template.Category ?? "Unknown",
            Status = template.Status ?? "Unknown",
            HeaderType = header?.Format,
            BodyText = bodyText,
            FooterText = footer?.Text,
            Placeholders = placeholders,
            ComponentsJson = JsonSerializer.Serialize(template.Components ?? [], SerializerOptions)
        };
    }

    private static IReadOnlyCollection<string> ExtractPlaceholders(string bodyText)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(bodyText, "\\{\\{\\d+\\}\\}");
        return matches.Select(match => match.Value).Distinct(StringComparer.Ordinal).ToList();
    }

    private static string SanitizeFailure(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Provider request failed.";
        }

        var trimmed = raw.Trim();
        return trimmed.Length <= 160 ? trimmed : $"{trimmed[..160]}...";
    }

    private sealed class WhatsAppCloudSendResponse
    {
        public IReadOnlyCollection<WhatsAppCloudSendMessage>? Messages { get; init; }
    }

    private sealed class WhatsAppCloudTemplatesResponse
    {
        public IReadOnlyCollection<WhatsAppCloudTemplate>? Data { get; init; }
    }

    private sealed class WhatsAppCloudTemplate
    {
        public string? Id { get; init; }
        public string? Name { get; init; }
        public string? Language { get; init; }
        public string? Category { get; init; }
        public string? Status { get; init; }
        public IReadOnlyCollection<WhatsAppCloudTemplateComponent>? Components { get; init; }
    }

    private sealed class WhatsAppCloudTemplateComponent
    {
        public string? Type { get; init; }
        public string? Format { get; init; }
        public string? Text { get; init; }
    }

    private sealed class WhatsAppCloudSendMessage
    {
        public string? Id { get; init; }
    }
}