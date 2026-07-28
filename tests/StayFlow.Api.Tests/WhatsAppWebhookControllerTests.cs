using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Controllers;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppWebhookControllerTests
{
    [Fact]
    public async Task Verify_WithValidToken_ReturnsChallenge()
    {
        var controller = CreateController(new FakeSignatureVerifier
        {
            VerifyTokenValid = true
        });

        var result = await controller.Verify("subscribe", "verify-token", "12345", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal("12345", content.Content);
    }

    [Fact]
    public async Task Verify_WithInvalidToken_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeSignatureVerifier
        {
            VerifyTokenValid = false
        });

        var result = await controller.Verify("subscribe", "wrong-token", "12345", CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Receive_WithValidSignature_QueuesPayloadAndReturnsOk()
    {
        var queue = new FakeQueue();
        var controller = CreateController(new FakeSignatureVerifier
        {
            SignatureResult = new WhatsAppWebhookSignatureValidationResult { IsValid = true }
        }, queue);
        var body = "{\"object\":\"whatsapp_business_account\",\"entry\":[]}";
        ConfigureRequest(controller, body, "sha256=abc123");

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.NotNull(queue.Envelope);
        Assert.Equal("whatsapp_business_account", queue.Envelope!.Payload.Object);
    }

    [Fact]
    public async Task Receive_WithMissingSignature_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeSignatureVerifier());
        ConfigureRequest(controller, "{\"object\":\"whatsapp_business_account\",\"entry\":[]}", null);

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Receive_WithMalformedJson_ReturnsBadRequest()
    {
        var controller = CreateController(new FakeSignatureVerifier
        {
            SignatureResult = new WhatsAppWebhookSignatureValidationResult { IsValid = true }
        });
        var body = "{\"object\":";
        ConfigureRequest(controller, body, "sha256=abc123");

        var result = await controller.Receive(CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Receive_WithOversizedPayload_ReturnsPayloadTooLarge()
    {
        var controller = CreateController(new FakeSignatureVerifier());
        var body = new string('x', 256 * 1024 + 1);
        ConfigureRequest(controller, body, "sha256=abc123");

        var result = await controller.Receive(CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status.StatusCode);
    }

    private static WhatsAppWebhookController CreateController(FakeSignatureVerifier signatureVerifier, FakeQueue? queue = null)
    {
        var options = Options.Create(new WhatsAppCloudOptions
        {
            Enabled = true
        });
        var controller = new WhatsAppWebhookController(
            options,
            signatureVerifier,
            queue ?? new FakeQueue(),
            NullLogger<WhatsAppWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return controller;
    }

    private static void ConfigureRequest(WhatsAppWebhookController controller, string body, string? signature)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        controller.ControllerContext.HttpContext.Request.Body = new MemoryStream(bytes);
        controller.ControllerContext.HttpContext.Request.ContentLength = bytes.Length;
        if (signature is not null)
        {
            controller.ControllerContext.HttpContext.Request.Headers["X-Hub-Signature-256"] = signature;
        }
    }

    private sealed class FakeQueue : IWhatsAppWebhookQueue
    {
        public QueuedWhatsAppWebhookEnvelope? Envelope { get; private set; }

        public ValueTask EnqueueAsync(QueuedWhatsAppWebhookEnvelope envelope, CancellationToken cancellationToken)
        {
            Envelope = envelope;
            return ValueTask.CompletedTask;
        }

        public ValueTask<QueuedWhatsAppWebhookEnvelope> DequeueAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    private sealed class FakeSignatureVerifier : IWhatsAppWebhookSignatureVerifier
    {
        public bool VerifyTokenValid { get; init; }
        public WhatsAppWebhookSignatureValidationResult SignatureResult { get; init; } = new()
        {
            IsValid = false,
            FailureReason = "InvalidSignature"
        };

        public Task<bool> IsWebhookVerificationTokenValidAsync(string? providedToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(VerifyTokenValid);
        }

        public Task<WhatsAppWebhookSignatureValidationResult> ValidateSignatureAsync(byte[] rawBody, string? signatureHeader, CancellationToken cancellationToken)
        {
            return Task.FromResult(SignatureResult);
        }
    }
}