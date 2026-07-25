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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", currentOptions.AccessToken);

        var endpoint = $"{currentOptions.GraphApiVersion.Trim('/')}/{request.PhoneNumberId}/messages";
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

    private sealed class WhatsAppCloudSendMessage
    {
        public string? Id { get; init; }
    }
}