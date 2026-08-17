using Microsoft.Extensions.Options;

namespace StayFlow.Api.Services.Payments;

public sealed class MpesaCredentialResolver(
    IConfiguration configuration,
    IHostEnvironment environment,
    IOptions<MpesaOptions> options) : IMpesaCredentialResolver
{
    private const string Prefix = "STAYFLOW_MPESA_";

    public Task<MpesaCredentialResolution> ResolveAsync(CancellationToken cancellationToken)
    {
        var reference = options.Value.DefaultCredentialReference;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Task.FromResult(new MpesaCredentialResolution
            {
                Success = false,
                FailureCode = "InvalidCredentialReference",
                FailureSummary = "M-PESA credential configuration is invalid."
            });
        }

        var normalizedReference = reference.Trim().ToUpperInvariant();

        var consumerKey = FirstNonEmpty(
            configuration[$"{Prefix}{normalizedReference}_CONSUMER_KEY"],
            Environment.GetEnvironmentVariable($"{Prefix}{normalizedReference}_CONSUMER_KEY"));

        var consumerSecret = FirstNonEmpty(
            configuration[$"{Prefix}{normalizedReference}_CONSUMER_SECRET"],
            Environment.GetEnvironmentVariable($"{Prefix}{normalizedReference}_CONSUMER_SECRET"));

        var passKey = FirstNonEmpty(
            configuration[$"{Prefix}{normalizedReference}_PASSKEY"],
            Environment.GetEnvironmentVariable($"{Prefix}{normalizedReference}_PASSKEY"));

        if (environment.IsDevelopment() && options.Value.DevelopmentMode)
        {
            consumerKey ??= "dev-consumer-key";
            consumerSecret ??= "dev-consumer-secret";
            passKey ??= "dev-passkey";
        }

        if (string.IsNullOrWhiteSpace(consumerKey) || string.IsNullOrWhiteSpace(consumerSecret))
        {
            return Task.FromResult(new MpesaCredentialResolution
            {
                Success = false,
                FailureCode = "MissingConsumerCredentials",
                FailureSummary = "M-PESA consumer key/secret are not configured."
            });
        }

        if (string.IsNullOrWhiteSpace(passKey))
        {
            return Task.FromResult(new MpesaCredentialResolution
            {
                Success = false,
                FailureCode = "MissingPassKey",
                FailureSummary = "M-PESA pass key is not configured."
            });
        }

        return Task.FromResult(new MpesaCredentialResolution
        {
            Success = true,
            ConsumerKey = consumerKey,
            ConsumerSecret = consumerSecret,
            PassKey = passKey
        });
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
