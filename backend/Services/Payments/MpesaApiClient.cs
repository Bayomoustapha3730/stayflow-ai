using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.Payments;

public sealed class MpesaApiClient(
    IHttpClientFactory httpClientFactory,
    IMpesaCredentialResolver credentialResolver,
    IOptions<MpesaOptions> options,
    IHostEnvironment hostEnvironment,
    ILogger<MpesaApiClient> logger) : IMpesaApiClient
{
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? cachedToken;
    private DateTimeOffset tokenExpiresAt;

    public async Task<MpesaStkPushResponse> InitiateStkPushAsync(
        MpesaStkPushRequest request,
        CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsDevelopment() && options.Value.DevelopmentMode)
        {
            return new MpesaStkPushResponse("development-merchant", $"development-{Guid.NewGuid():N}", 0, "Accepted", "Request accepted");
        }

        var token = await GetAccessTokenAsync(cancellationToken);
        var client = httpClientFactory.CreateClient(nameof(MpesaApiClient));
        using var message = new HttpRequestMessage(HttpMethod.Post, "mpesa/stkpush/v1/processrequest")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Daraja STK Push returned HTTP status {StatusCode}.", (int)response.StatusCode);
            throw new MpesaProviderException("Safaricom did not accept the payment request.");
        }

        var payload = await response.Content.ReadFromJsonAsync<MpesaStkPushResponse>(cancellationToken);
        return payload ?? throw new MpesaProviderException("Safaricom returned an empty payment response.");
    }

    public async Task<MpesaStkQueryResponse> QueryStkPushAsync(
        MpesaStkQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (hostEnvironment.IsDevelopment() && options.Value.DevelopmentMode)
        {
            return new MpesaStkQueryResponse(
                0,
                "The service request has been accepted successfully",
                "development-merchant",
                request.CheckoutRequestId,
                0,
                "The service request is processed successfully.");
        }

        var token = await GetAccessTokenAsync(cancellationToken);
        var client = httpClientFactory.CreateClient(nameof(MpesaApiClient));

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "mpesa/stkpushquery/v1/query")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response =
            await SendWithRetryAsync(client, message, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Daraja STK Query returned HTTP status {StatusCode}.",
                (int)response.StatusCode);

            throw new MpesaProviderException(
                "Safaricom did not accept the STK status query.");
        }

        var payload =
            await response.Content.ReadFromJsonAsync<MpesaStkQueryResponse>(
                cancellationToken);

        return payload
            ?? throw new MpesaProviderException(
                "Safaricom returned an empty STK status response.");
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (cachedToken is not null && tokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(options.Value.TokenExpirySkewSeconds))
        {
            return cachedToken;
        }

        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (cachedToken is not null && tokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(options.Value.TokenExpirySkewSeconds))
            {
                return cachedToken;
            }

            var credentials = await credentialResolver.ResolveAsync(cancellationToken);
            if (!credentials.Success || credentials.ConsumerKey is null || credentials.ConsumerSecret is null)
            {
                throw new MpesaProviderException(credentials.FailureSummary ?? "M-PESA credentials are not configured.");
            }

            var client = httpClientFactory.CreateClient(nameof(MpesaApiClient));
            using var request = new HttpRequestMessage(HttpMethod.Get, "oauth/v1/generate?grant_type=client_credentials");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.ConsumerKey}:{credentials.ConsumerSecret}")));
            using var response = await SendWithRetryAsync(client, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Daraja OAuth returned HTTP status {StatusCode}.", (int)response.StatusCode);
                throw new MpesaProviderException("Safaricom authentication failed.");
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<MpesaTokenResponse>(cancellationToken);
            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new MpesaProviderException("Safaricom returned an invalid authentication response.");
            }

            cachedToken = tokenResponse.AccessToken;
            tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds);
            return cachedToken;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var clonedRequest = await CloneRequestAsync(request, cancellationToken);
            try
            {
                var response = await client.SendAsync(clonedRequest, cancellationToken);
                if (response.IsSuccessStatusCode || attempt >= options.Value.MaxRetryAttempts || (int)response.StatusCode < 500)
                {
                    return response;
                }

                response.Dispose();
            }
            catch (HttpRequestException) when (attempt < options.Value.MaxRetryAttempts)
            {
            }

            await Task.Delay(options.Value.RetryBaseDelayMilliseconds * (attempt + 1), cancellationToken);
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage source, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(source.Method, source.RequestUri);
        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Content is not null)
        {
            var content = await source.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(content);
            foreach (var header in source.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private sealed record MpesaTokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresInSeconds);
}

public sealed class MpesaProviderException(string message) : Exception(message);
