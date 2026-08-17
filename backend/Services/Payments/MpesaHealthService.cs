using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StayFlow.Api.DTOs.Payments;

namespace StayFlow.Api.Services.Payments;

public interface IMpesaHealthService
{
    Task<MpesaHealthResponse> CheckAsync(CancellationToken cancellationToken);
}

/// <summary>
/// M-PESA configuration/connectivity health check. Never initiates an STK Push and never
/// exposes credential values or Authorization headers in results or logs.
/// </summary>
public sealed class MpesaHealthService(
    IOptions<MpesaOptions> options,
    IMpesaCredentialResolver credentialResolver,
    IHttpClientFactory httpClientFactory,
    ILogger<MpesaHealthService> logger) : IMpesaHealthService
{
    public async Task<MpesaHealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;

        if (!options.Value.Enabled)
        {
            return Complete("Disabled", "M-PESA integration is disabled.", false, checkedAt);
        }

        if (string.IsNullOrWhiteSpace(options.Value.ShortCode) || string.IsNullOrWhiteSpace(options.Value.CallbackBaseUrl))
        {
            return Complete("ConfigurationMissing", "M-PESA short code or callback URL is not configured.", false, checkedAt);
        }

        var credentials = await credentialResolver.ResolveAsync(cancellationToken);
        if (!credentials.Success)
        {
            return Complete("ConfigurationMissing", credentials.FailureSummary ?? "M-PESA credentials are not configured.", false, checkedAt);
        }

        try
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Min(options.Value.RequestTimeoutSeconds, 5)));

            var client = httpClientFactory.CreateClient(nameof(MpesaHealthService));
            var authValue = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{credentials.ConsumerKey}:{credentials.ConsumerSecret}"));

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{options.Value.BaseUrl.TrimEnd('/')}/oauth/v1/generate?grant_type=client_credentials");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            using var response = await client.SendAsync(request, timeoutSource.Token);

            return response.IsSuccessStatusCode
                ? Complete("ProviderReachable", "Safaricom Daraja OAuth endpoint is reachable.", true, checkedAt)
                : Complete("ProviderUnavailable", $"Safaricom Daraja returned status {(int)response.StatusCode}.", false, checkedAt);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("M-PESA health check timed out contacting the Daraja OAuth endpoint.");
            return Complete("ProviderUnavailable", "Safaricom Daraja did not respond in time.", false, checkedAt);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "M-PESA health check failed to reach the Daraja OAuth endpoint.");
            return Complete("ProviderUnavailable", "Safaricom Daraja is temporarily unreachable.", false, checkedAt);
        }
    }

    private static MpesaHealthResponse Complete(string status, string message, bool isOperational, DateTimeOffset checkedAt) => new()
    {
        Status = status,
        Message = message,
        IsOperational = isOperational,
        CheckedAt = checkedAt
    };
}
