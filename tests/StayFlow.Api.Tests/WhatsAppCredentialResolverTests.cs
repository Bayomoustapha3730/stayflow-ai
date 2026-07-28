using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppCredentialResolverTests
{
    [Fact]
    public async Task ResolveAsync_UsesEnvironmentReferenceVariables()
    {
        const string reference = "company-main";
        Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_COMPANY_MAIN_ACCESS_TOKEN", "token-123");
        Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_COMPANY_MAIN_APP_SECRET", "secret-456");
        Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_COMPANY_MAIN_WEBHOOK_VERIFY_TOKEN", "verify-789");

        try
        {
            var resolver = CreateResolver(isDevelopment: false);
            var result = await resolver.ResolveAsync(new WhatsAppIntegration
            {
                CredentialReference = reference
            }, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("token-123", result.AccessToken);
            Assert.Equal("secret-456", result.AppSecret);
            Assert.Equal("verify-789", result.WebhookVerifyToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_COMPANY_MAIN_ACCESS_TOKEN", null);
            Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_COMPANY_MAIN_APP_SECRET", null);
            Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_COMPANY_MAIN_WEBHOOK_VERIFY_TOKEN", null);
        }
    }

    [Fact]
    public async Task ResolveAsync_MissingAccessToken_ReturnsStructuredFailure()
    {
        Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_MISSING_APP_SECRET", "secret");
        Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_MISSING_WEBHOOK_VERIFY_TOKEN", "verify");

        try
        {
            var resolver = CreateResolver(isDevelopment: false);
            var result = await resolver.ResolveAsync(new WhatsAppIntegration
            {
                CredentialReference = "missing"
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("MissingAccessToken", result.FailureCode);
            Assert.Contains("missing", result.FailureSummary ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_MISSING_APP_SECRET", null);
            Environment.SetEnvironmentVariable("STAYFLOW_WHATSAPP_MISSING_WEBHOOK_VERIFY_TOKEN", null);
        }
    }

    [Fact]
    public async Task ResolveAsync_DevelopmentMode_ReturnsDeterministicFallbackCredentials()
    {
        var resolver = CreateResolver(isDevelopment: true);

        var result = await resolver.ResolveAsync(new WhatsAppIntegration
        {
            CredentialReference = "dev-reference"
        }, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("dev-access-token", result.AccessToken);
        Assert.Equal("dev-app-secret", result.AppSecret);
        Assert.Equal("dev-verify-token", result.WebhookVerifyToken);
    }

    private static WhatsAppCredentialResolver CreateResolver(bool isDevelopment)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = Options.Create(new WhatsAppCloudOptions
        {
            DefaultCredentialReference = "default"
        });

        return new WhatsAppCredentialResolver(
            configuration,
            new TestHostEnvironment(isDevelopment),
            options);
    }

    private sealed class TestHostEnvironment(bool isDevelopment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isDevelopment ? "Development" : "Production";
        public string ApplicationName { get; set; } = "StayFlow.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
