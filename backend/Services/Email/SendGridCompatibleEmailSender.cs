using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StayFlow.Api.Configuration;

namespace StayFlow.Api.Services.Email;

public sealed class SendGridCompatibleEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<EmailDeliveryOptions> options) : IEmailSender
{
    private readonly EmailDeliveryOptions emailOptions = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(emailOptions.SendGrid.BaseUrl.TrimEnd('/') + "/"), emailOptions.SendGrid.SendPath.TrimStart('/')))
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                personalizations = new[]
                {
                    new
                    {
                        to = new[] { new { email = message.ToAddress } }
                    }
                },
                from = new { email = emailOptions.FromAddress, name = emailOptions.FromName },
                subject = message.Subject,
                content = new object[]
                {
                    new { type = "text/plain", value = message.PlainTextBody },
                    new { type = "text/html", value = string.IsNullOrWhiteSpace(message.HtmlBody) ? message.PlainTextBody : message.HtmlBody }
                }
            }), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", emailOptions.SendGrid.ApiKey);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}