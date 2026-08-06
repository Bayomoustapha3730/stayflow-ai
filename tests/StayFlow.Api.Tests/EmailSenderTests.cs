using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Options;
using StayFlow.Api.Configuration;
using StayFlow.Api.Services.Email;

namespace StayFlow.Api.Tests;

public sealed class EmailSenderTests
{
    [Fact]
    public async Task DevelopmentEmailSender_StoresMessageInInbox()
    {
        var inbox = new DevelopmentEmailInbox();
        var sender = new DevelopmentEmailSender(inbox);

        await sender.SendAsync(new EmailMessage
        {
            ToAddress = "guest@example.test",
            Subject = "Subject",
            PlainTextBody = "Hello"
        }, CancellationToken.None);

        Assert.Single(inbox.Messages);
        Assert.Equal("guest@example.test", inbox.Messages.Single().ToAddress);
    }

    [Fact]
    public async Task SendGridCompatibleEmailSender_SendsExpectedRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });

        var sender = new SendGridCompatibleEmailSender(
            new SingleClientFactory(new HttpClient(handler)),
            Options.Create(new EmailDeliveryOptions
            {
                FromAddress = "no-reply@example.test",
                FromName = "StayFlow",
                SendGrid = new SendGridCompatibleEmailOptions
                {
                    BaseUrl = "https://api.sendgrid.test",
                    ApiKey = "sg-key"
                }
            }));

        await sender.SendAsync(new EmailMessage
        {
            ToAddress = "guest@example.test",
            Subject = "Hello",
            PlainTextBody = "Plain",
            HtmlBody = "<p>Plain</p>"
        }, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.sendgrid.test/v3/mail/send", capturedRequest.RequestUri?.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task AzureCommunicationServicesCompatibleEmailSender_SendsExpectedRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new DelegatingHandlerStub((request, _) =>
        {
            capturedRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        });

        var sender = new AzureCommunicationServicesCompatibleEmailSender(
            new SingleClientFactory(new HttpClient(handler)),
            Options.Create(new EmailDeliveryOptions
            {
                FromAddress = "no-reply@example.test",
                AzureCommunicationServices = new AzureCommunicationServicesEmailOptions
                {
                    Endpoint = "https://acs.example.test",
                    ApiKey = "acs-key",
                    ApiVersion = "2023-03-31"
                }
            }));

        await sender.SendAsync(new EmailMessage
        {
            ToAddress = "guest@example.test",
            Subject = "Hello",
            PlainTextBody = "Plain"
        }, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://acs.example.test/emails:send?api-version=2023-03-31", capturedRequest.RequestUri?.ToString());
        Assert.True(capturedRequest.Headers.Contains("api-key"));
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}