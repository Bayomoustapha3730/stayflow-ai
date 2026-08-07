using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.Configuration;

namespace StayFlow.Api.Services.Email;

public sealed class AzureCommunicationServicesCompatibleEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailDeliveryOptions> options) : IEmailSender
{
    private readonly EmailDeliveryOptions emailOptions = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var endpoint = emailOptions.AzureCommunicationServices.Endpoint.TrimEnd('/');
        var apiVersion = Uri.EscapeDataString(emailOptions.AzureCommunicationServices.ApiVersion);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/emails:send?api-version={apiVersion}")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                senderAddress = emailOptions.FromAddress,
                recipients = new
                {
                    to = new[]
                    {
                        new { address = message.ToAddress }
                    }
                },
                content = new
                {
                    subject = message.Subject,
                    plainText = message.PlainTextBody,
                    html = string.IsNullOrWhiteSpace(message.HtmlBody) ? message.PlainTextBody : message.HtmlBody
                }
            }), Encoding.UTF8, "application/json")
        };

        request.Headers.Add("api-key", emailOptions.AzureCommunicationServices.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}