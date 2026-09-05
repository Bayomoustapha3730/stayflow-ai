using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppCloudClientTests
{
    [Fact]
    public async Task SendTextMessageAsync_UsesVersionPhoneAndBearerAndParsesMessageId()
    {
        Uri? requestUri = null;
        string? authScheme = null;
        string? authValue = null;
        string? payload = null;
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            requestUri = request.RequestUri;
            authScheme = request.Headers.Authorization?.Scheme;
            authValue = request.Headers.Authorization?.Parameter;
            payload = request.Content is null ? null : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.123\"}]}", Encoding.UTF8, "application/json")
            });
        });

        var client = CreateClient(handler, new WhatsAppCloudOptions
        {
            GraphApiBaseUrl = "https://graph.facebook.com",
            ProductionSendingEnabled = true,
            RequestTimeoutSeconds = 10,
            MaxPostRetryAttempts = 0
        });

        var result = await client.SendTextMessageAsync(new WhatsAppSendTextMessageRequest
        {
            CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            IntegrationId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            IsIntegrationProductionEnabled = true,
            Origin = WhatsAppSendOrigin.ManualHost,
            AccessToken = "access-token",
            GraphApiVersion = "v23.0",
            PhoneNumberId = "999999",
            To = "+14155550123",
            Body = "Hello",
            ClientMessageId = "abc"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("wamid.123", result.ExternalMessageId);
        Assert.Equal("https://graph.facebook.com/v23.0/999999/messages", requestUri?.ToString());
        Assert.Equal("Bearer", authScheme);
        Assert.Equal("access-token", authValue);
        Assert.NotNull(payload);
        Assert.Contains("\"messaging_product\":\"whatsapp\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"recipient_type\":\"individual\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"text\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"preview_url\":false", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendTextMessageAsync_RateLimit_IsClassifiedSafely()
    {
        var handler = new DelegatingHandlerStub((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Rate limit exceeded\",\"code\":130429,\"is_transient\":true}}", Encoding.UTF8, "application/json")
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return Task.FromResult(response);
        });

        var client = CreateClient(handler, new WhatsAppCloudOptions
        {
            GraphApiBaseUrl = "https://graph.facebook.com",
            ProductionSendingEnabled = true,
            RequestTimeoutSeconds = 10,
            MaxPostRetryAttempts = 0
        });

        var result = await client.SendTextMessageAsync(new WhatsAppSendTextMessageRequest
        {
            CompanyId = Guid.NewGuid(),
            IntegrationId = Guid.NewGuid(),
            IsIntegrationProductionEnabled = true,
            Origin = WhatsAppSendOrigin.ManualHost,
            AccessToken = "access-token-should-not-leak",
            GraphApiVersion = "v23.0",
            PhoneNumberId = "999999",
            To = "+14155550123",
            Body = "Hello",
            ClientMessageId = "abc"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("RateLimited", result.FailureCategory);
        Assert.Equal("WhatsApp is temporarily rate limited. Try again shortly.", result.FailureReason);
        Assert.DoesNotContain("access-token-should-not-leak", result.FailureReason ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendTemplateMessageAsync_GlobalProductionFlagDisabled_DoesNotInvokeProvider()
    {
        var calls = 0;
        var client = CreateClient(new DelegatingHandlerStub((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }), new WhatsAppCloudOptions
        {
            GraphApiBaseUrl = "https://graph.facebook.com",
            ProductionSendingEnabled = false
        });

        var result = await client.SendTemplateMessageAsync(new WhatsAppTemplateSendRequest
        {
            CompanyId = Guid.NewGuid(),
            IntegrationId = Guid.NewGuid(),
            IsIntegrationProductionEnabled = true,
            Origin = WhatsAppSendOrigin.TemplateManual,
            AccessToken = "token",
            GraphApiVersion = "v23.0",
            PhoneNumberId = "999999",
            To = "+14155550123",
            TemplateName = "approved_template",
            LanguageCode = "en_US",
            ClientMessageId = "abc"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ProductionSendingDisabled", result.FailureCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task SendTextMessageAsync_IntegrationProductionFlagDisabled_DoesNotInvokeProviderInDevelopment()
    {
        var calls = 0;
        var client = CreateClient(new DelegatingHandlerStub((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }), new WhatsAppCloudOptions
        {
            GraphApiBaseUrl = "https://graph.facebook.com",
            ProductionSendingEnabled = true,
            DevelopmentMode = false
        });

        var result = await client.SendTextMessageAsync(new WhatsAppSendTextMessageRequest
        {
            CompanyId = Guid.NewGuid(),
            IntegrationId = Guid.NewGuid(),
            IsIntegrationProductionEnabled = false,
            Origin = WhatsAppSendOrigin.ManualHost,
            AccessToken = "token",
            GraphApiVersion = "v23.0",
            PhoneNumberId = "999999",
            To = "+14155550123",
            Body = "Hello",
            ClientMessageId = "abc"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("ProductionSendingDisabled", result.FailureCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task GetTemplatesAsync_HandlesProviderPagination()
    {
        var calls = 0;
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            calls++;

            if (calls == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"data\":[{\"id\":\"t1\",\"name\":\"welcome\",\"language\":\"en_US\",\"category\":\"UTILITY\",\"status\":\"APPROVED\",\"components\":[{\"type\":\"BODY\",\"text\":\"Hi {{1}}\"}]}],\"paging\":{\"next\":\"https://graph.facebook.com/v23.0/waba/message_templates?after=cursor-1\"}}",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"data\":[{\"id\":\"t2\",\"name\":\"checkin\",\"language\":\"en_US\",\"category\":\"UTILITY\",\"status\":\"PENDING\",\"components\":[{\"type\":\"BODY\",\"text\":\"Code {{1}}\"}]}]}",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        var client = CreateClient(handler, new WhatsAppCloudOptions
        {
            GraphApiBaseUrl = "https://graph.facebook.com",
            RequestTimeoutSeconds = 10,
            MaxRetryAttempts = 0,
            MaxTemplateSyncPages = 5,
            MaxTemplateSyncItems = 100
        });

        var result = await client.GetTemplatesAsync(new WhatsAppGetTemplatesRequest
        {
            CompanyId = Guid.NewGuid(),
            IntegrationId = Guid.NewGuid(),
            AccessToken = "token",
            GraphApiVersion = "v23.0",
            WhatsAppBusinessAccountId = "waba"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Templates.Count);
        Assert.Equal(2, calls);
    }

    private static WhatsAppCloudClient CreateClient(HttpMessageHandler handler, WhatsAppCloudOptions cloudOptions)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(cloudOptions.GraphApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(cloudOptions.RequestTimeoutSeconds)
        };

        return new WhatsAppCloudClient(
            new SingleClientFactory(httpClient),
            Options.Create(cloudOptions),
            new WhatsAppOutboundSendGate(Options.Create(cloudOptions)),
            new NullTelemetry(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<WhatsAppCloudClient>.Instance);
    }

    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class NullTelemetry : IWhatsAppProviderTelemetry
    {
        public void RecordHealthResult(Guid companyId, Guid integrationId, string status, bool success, long elapsedMilliseconds)
        {
        }

        public void RecordRateLimit(Guid companyId, Guid integrationId, string operation, int? retryAfterSeconds)
        {
        }

        public void RecordRetry(Guid companyId, Guid integrationId, string operation, int attempt, int? httpStatusCode)
        {
        }

        public void RecordSendResult(Guid companyId, Guid integrationId, string messageType, bool success, string? category, int? httpStatusCode, int attempts, long elapsedMilliseconds, string? supportReference)
        {
        }

        public void RecordTemplateSyncResult(Guid companyId, Guid integrationId, bool success, string? category, int attempts, long elapsedMilliseconds)
        {
        }
    }
}
