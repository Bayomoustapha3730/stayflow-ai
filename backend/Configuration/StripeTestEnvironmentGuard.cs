using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Services.Billing;

namespace StayFlow.Api.Configuration;

public sealed class StripeTestEnvironmentGuard
{
    public const string EnvironmentIdentityKey = "StayFlow:EnvironmentIdentity";
    public const string StripeTestEnvironmentIdentity = "StripeTest";

    private StripeTestEnvironmentGuard(bool isStripeTestEnvironment)
    {
        IsStripeTestEnvironment = isStripeTestEnvironment;
    }

    public bool IsStripeTestEnvironment { get; }

    public static StripeTestEnvironmentGuard ValidateAndCreate(
        IConfiguration configuration,
        string environmentName)
    {
        var identity = configuration[EnvironmentIdentityKey];
        if (identity is null)
        {
            return new StripeTestEnvironmentGuard(false);
        }

        if (!string.Equals(identity, StripeTestEnvironmentIdentity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{EnvironmentIdentityKey} must be {StripeTestEnvironmentIdentity} when specified.");
        }

        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{EnvironmentIdentityKey} cannot be combined with ASPNETCORE_ENVIRONMENT=Development.");
        }

        ValidateDatabaseTarget(configuration);
        ValidateStripeConfiguration(configuration);
        ValidateDisabledUsageBilling(configuration);

        return new StripeTestEnvironmentGuard(true);
    }

    public void EnsureExternalWriteAuthorized()
    {
        if (IsStripeTestEnvironment)
        {
            throw new ExternalDependencyException(
                "Stripe test mode has not been authenticated; external writes are disabled.",
                "stripe_test_mode_unverified");
        }
    }

    public void ValidateWebhookMode(JsonElement eventRoot)
    {
        if (!IsStripeTestEnvironment)
        {
            return;
        }

        if (eventRoot.ValueKind != JsonValueKind.Object)
        {
            throw new ForbiddenOperationException(
                "Stripe test environment requires an event explicitly marked as test mode.",
                "stripe_webhook_mode_mismatch");
        }

        var foundLiveMode = false;
        var liveMode = default(JsonElement);
        foreach (var property in eventRoot.EnumerateObject())
        {
            if (!property.NameEquals("livemode"))
            {
                continue;
            }

            if (foundLiveMode)
            {
                throw new ForbiddenOperationException(
                    "Stripe test environment requires an unambiguous event mode.",
                    "stripe_webhook_mode_mismatch");
            }

            foundLiveMode = true;
            liveMode = property.Value;
        }

        if (!foundLiveMode
            || liveMode.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || liveMode.GetBoolean())
        {
            throw new ForbiddenOperationException(
                "Stripe test environment requires an event explicitly marked as test mode.",
                "stripe_webhook_mode_mismatch");
        }
    }

    private static void ValidateDatabaseTarget(IConfiguration configuration)
    {
        var approvedHost = configuration["StayFlow:StripeTest:Database:ApprovedHost"];
        var approvedDatabase = configuration["StayFlow:StripeTest:Database:ApprovedDatabase"];
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(approvedHost)
            || string.IsNullOrWhiteSpace(approvedDatabase)
            || string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Stripe test environment requires an approved database host, database name, and connection target.");
        }

        NpgsqlConnectionStringBuilder target;
        try
        {
            target = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                "Stripe test database connection target is not a valid Npgsql connection string.");
        }

        IReadOnlyList<ConnectionStringEntry> entries;
        try
        {
            entries = ParseConnectionStringEntries(connectionString);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Stripe test database connection target has malformed key/value structure.");
        }

        var targetHost = target.Host ?? string.Empty;
        var targetDatabase = target.Database ?? string.Empty;
        var hostEntries = entries.Where(entry => IsHostKey(entry.Key)).ToArray();
        var databaseEntries = entries.Where(entry => IsDatabaseKey(entry.Key)).ToArray();
        var hostValues = hostEntries.Select(GetHostValue).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var databaseValues = databaseEntries.Select(GetDatabaseValue).Distinct(StringComparer.Ordinal).ToArray();
        if (hostEntries.Length == 0
            || databaseEntries.Length == 0
            || HasDuplicateKeys(hostEntries)
            || HasDuplicateKeys(databaseEntries)
            || hostValues.Length != 1
            || databaseValues.Length != 1
            || entries.Any(entry => IsFallbackTargetKey(entry.Key))
            || target.LoadBalanceHosts
            || targetHost.Contains(',', StringComparison.Ordinal)
            || !string.Equals(hostValues[0], targetHost, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(databaseValues[0], targetDatabase, StringComparison.Ordinal)
            || !string.Equals(targetHost, approvedHost, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(targetDatabase, approvedDatabase, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Stripe test database connection target does not match the approved host and database.");
        }
    }

    private static bool HasDuplicateKeys(IEnumerable<ConnectionStringEntry> entries) =>
        entries.GroupBy(entry => NormalizeConnectionStringKey(entry.Key), StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

    private static string GetHostValue(ConnectionStringEntry entry) =>
        new NpgsqlConnectionStringBuilder($"{entry.Key}={entry.Value}").Host ?? string.Empty;

    private static string GetDatabaseValue(ConnectionStringEntry entry) =>
        new NpgsqlConnectionStringBuilder($"{entry.Key}={entry.Value}").Database ?? string.Empty;

    private static IReadOnlyList<ConnectionStringEntry> ParseConnectionStringEntries(string connectionString)
    {
        var entries = new List<ConnectionStringEntry>();
        var position = 0;
        while (position < connectionString.Length)
        {
            while (position < connectionString.Length
                && (char.IsWhiteSpace(connectionString[position]) || connectionString[position] == ';'))
            {
                position++;
            }

            if (position == connectionString.Length)
            {
                break;
            }

            var keyStart = position;
            while (position < connectionString.Length && connectionString[position] is not ('=' or ';'))
            {
                position++;
            }

            if (position == connectionString.Length || connectionString[position] != '=')
            {
                throw new FormatException("Connection string key/value pair is malformed.");
            }

            var key = connectionString[keyStart..position].Trim();
            if (key.Length == 0)
            {
                throw new FormatException("Connection string key/value pair is malformed.");
            }

            position++;
            while (position < connectionString.Length && char.IsWhiteSpace(connectionString[position]))
            {
                position++;
            }

            var valueStart = position;
            int valueEnd;
            if (position < connectionString.Length && connectionString[position] is '\'' or '"')
            {
                var quote = connectionString[position++];
                var closed = false;
                while (position < connectionString.Length)
                {
                    if (connectionString[position] != quote)
                    {
                        position++;
                        continue;
                    }

                    if (position + 1 < connectionString.Length && connectionString[position + 1] == quote)
                    {
                        position += 2;
                        continue;
                    }

                    position++;
                    closed = true;
                    break;
                }

                if (!closed)
                {
                    throw new FormatException("Connection string value is malformed.");
                }

                valueEnd = position;
                while (position < connectionString.Length && char.IsWhiteSpace(connectionString[position]))
                {
                    position++;
                }

                if (position < connectionString.Length && connectionString[position] != ';')
                {
                    throw new FormatException("Connection string value is malformed.");
                }
            }
            else
            {
                while (position < connectionString.Length && connectionString[position] != ';')
                {
                    position++;
                }

                valueEnd = position;
            }

            entries.Add(new ConnectionStringEntry(
                key,
                connectionString[valueStart..valueEnd].Trim()));

            if (position < connectionString.Length)
            {
                position++;
            }
        }

        return entries;
    }

    private static bool IsHostKey(string key) =>
        NormalizeConnectionStringKey(key) is "host" or "server";

    private static bool IsDatabaseKey(string key) =>
        NormalizeConnectionStringKey(key) is "database" or "initialcatalog";

    private static bool IsFallbackTargetKey(string key) =>
        NormalizeConnectionStringKey(key) is "service" or "servicefile" or "hostaddr"
            or "targetsessionattributes";

    private static string NormalizeConnectionStringKey(string key)
    {
        var normalized = new StringBuilder(key.Length);
        foreach (var character in key)
        {
            if (!char.IsWhiteSpace(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString();
    }

    private readonly record struct ConnectionStringEntry(string Key, string Value);

    private static void ValidateStripeConfiguration(IConfiguration configuration)
    {
        if (!string.Equals(configuration["Billing:Provider"], "Stripe", StringComparison.OrdinalIgnoreCase)
            || !bool.TryParse(configuration["Billing:StripeTestModeExpected"], out var testModeExpected)
            || !testModeExpected)
        {
            throw new InvalidOperationException(
                "Stripe test environment requires Billing:Provider=Stripe and Billing:StripeTestModeExpected=true.");
        }

        var secretKey = configuration["Billing:StripeSecretKey"];
        var normalizedSecretKey = secretKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSecretKey)
            || normalizedSecretKey.StartsWith("sk_live_", StringComparison.OrdinalIgnoreCase)
            || normalizedSecretKey.StartsWith("rk_live_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Stripe test environment requires a configured non-live Stripe credential.");
        }

        if (string.IsNullOrWhiteSpace(configuration["Billing:StripeWebhookSigningSecret"])
            || !string.Equals(
                configuration["Billing:StripeApiVersion"],
                BillingOptions.SupportedStripeApiVersion,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(configuration["Billing:PlanPriceIds:Starter"])
            || string.IsNullOrWhiteSpace(configuration["Billing:PlanPriceIds:Professional"]))
        {
            throw new InvalidOperationException(
                "Stripe test environment requires webhook, supported API-version, and plan-price configuration.");
        }
    }

    private static void ValidateDisabledUsageBilling(IConfiguration configuration)
    {
        foreach (var key in new[]
        {
            "Billing:UsageBilling:Enabled",
            "Billing:UsageBilling:Worker:Enabled",
            "Billing:UsageBilling:OpenPeriodInvoiceSafetyVerified"
        })
        {
            if (!bool.TryParse(configuration[key], out var enabled) || enabled)
            {
                throw new InvalidOperationException(
                    $"Stripe test environment requires {key}=false explicitly.");
            }
        }
    }
}
