using Microsoft.Extensions.Configuration;
using StayFlow.Api.Configuration;

namespace StayFlow.Api.Tests;

public sealed class StripeTestEnvironmentGuardTests
{
    private const string ApprovedHost = "pg-test.internal";
    private const string ApprovedDatabase = "stayflow_test";

    [Fact]
    public void MissingMarker_PreservesExistingEnvironmentBehavior()
    {
        var guard = StripeTestEnvironmentGuard.ValidateAndCreate(
            new ConfigurationBuilder().Build(),
            "Development");

        Assert.False(guard.IsStripeTestEnvironment);
    }

    [Fact]
    public void ValidMarker_EnablesStripeTestGuard()
    {
        var guard = CreateGuard();

        Assert.True(guard.IsStripeTestEnvironment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Development")]
    public void MissingOrNonStripeProvider_IsRejected(string? provider)
    {
        var values = ValidValues();
        if (provider is null)
        {
            values.Remove("Billing:Provider");
        }
        else
        {
            values["Billing:Provider"] = provider;
        }

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Fact]
    public void UnknownMarker_IsRejected()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(("StayFlow:EnvironmentIdentity", "Test")),
                "Production"));

        Assert.Contains("StayFlow:EnvironmentIdentity", error.Message);
    }

    [Theory]
    [InlineData("StayFlow:StripeTest:Database:ApprovedHost")]
    [InlineData("StayFlow:StripeTest:Database:ApprovedDatabase")]
    public void MissingApprovedDatabaseTarget_IsRejected(string missingKey)
    {
        var values = ValidValues();
        values.Remove(missingKey);

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Theory]
    [InlineData("Host=prod-db.internal;Database=stayflow_test;Username=test")]
    [InlineData("Host=pg-test.internal;Database=stayflow_production;Username=test")]
    [InlineData("Host=pg-test.internal,prod-db.internal;Database=stayflow_test;Username=test")]
    public void MismatchedOrMultiHostDatabaseTarget_IsRejected(string connectionString)
    {
        var values = ValidValues();
        values["ConnectionStrings:DefaultConnection"] = connectionString;

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Theory]
    [InlineData("Server=pg-test.internal;Database=stayflow_test;Username=test")]
    [InlineData("Host=pg-test.internal;Server=pg-test.internal;Database=stayflow_test")]
    public void SupportedUnambiguousConnectionStringAliases_AreAccepted(string connectionString)
    {
        var values = ValidValues();
        values["ConnectionStrings:DefaultConnection"] = connectionString;

        var guard = StripeTestEnvironmentGuard.ValidateAndCreate(
            BuildConfiguration(values),
            "Production");

        Assert.True(guard.IsStripeTestEnvironment);
    }

    [Theory]
    [InlineData("Host=pg-test.internal;Host=pg-test.internal;Database=stayflow_test")]
    [InlineData("Database=stayflow_test;Database=stayflow_test;Host=pg-test.internal")]
    [InlineData("Database=stayflow_test;Database=stayflow_production;Host=pg-test.internal")]
    [InlineData("Initial Catalog=stayflow_test;Initial Catalog=stayflow_test;Host=pg-test.internal")]
    [InlineData("Host=pg-test.internal;Server=prod-db.internal;Database=stayflow_test")]
    [InlineData("Host=pg-test.internal;Database=stayflow_test;Initial Catalog=stayflow_production")]
    [InlineData("Host=pg-test.internal;Database=stayflow_test;HostAddr=10.0.0.5")]
    [InlineData("Host=pg-test.internal;Database=stayflow_test;Service=prod")]
    [InlineData("Host=pg-test.internal;Database=stayflow_test;Target Session Attributes=read-write")]
    public void DuplicateConflictingOrFallbackConnectionTargets_AreRejected(string connectionString)
    {
        var values = ValidValues();
        values["ConnectionStrings:DefaultConnection"] = connectionString;

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Host=pg-test.internal;Username=test")]
    public void MissingConfiguredDatabaseTarget_IsRejected(string? connectionString)
    {
        var values = ValidValues();
        if (connectionString is null)
        {
            values.Remove("ConnectionStrings:DefaultConnection");
        }
        else
        {
            values["ConnectionStrings:DefaultConnection"] = connectionString;
        }

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Fact]
    public void MalformedConnectionStringError_DoesNotRevealSecretBearingFragments()
    {
        const string sentinel = "redaction-sentinel-not-a-secret";
        var values = ValidValues();
        values["ConnectionStrings:DefaultConnection"] =
            $"Host=pg-test.internal;Database=stayflow_test;Password={sentinel};UnknownOption=value";

        var error = Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));

        Assert.DoesNotContain(sentinel, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedConnectionStringDoesNotLeakSentinelToExceptionOrDiagnostics()
    {
        const string sentinel = "synthetic-redaction-sentinel";
        var values = ValidValues();
        values["ConnectionStrings:DefaultConnection"] =
            $"Host=pg-test.internal;Database=stayflow_test;Password='{sentinel}';UnknownOption";
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var errorOutput = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(errorOutput);
            var exception = Assert.Throws<InvalidOperationException>(() =>
                StripeTestEnvironmentGuard.ValidateAndCreate(
                    BuildConfiguration(values),
                    "Production"));

            Assert.DoesNotContain(sentinel, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, errorOutput.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Theory]
    [InlineData("sk_live_placeholder")]
    [InlineData("rk_live_placeholder")]
    public void LiveCredentialIndication_IsRejectedWithoutEchoingValue(string credential)
    {
        var values = ValidValues();
        values["Billing:StripeSecretKey"] = credential;

        var error = Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));

        Assert.DoesNotContain(credential, error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Billing:UsageBilling:Enabled")]
    [InlineData("Billing:UsageBilling:Worker:Enabled")]
    [InlineData("Billing:UsageBilling:OpenPeriodInvoiceSafetyVerified")]
    public void EnabledUsageBillingSafetyFlag_IsRejected(string key)
    {
        var values = ValidValues();
        values[key] = "true";

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Fact]
    public void MissingUsageBillingSafetyFlag_IsRejected()
    {
        var values = ValidValues();
        values.Remove("Billing:UsageBilling:Worker:Enabled");

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Fact]
    public void StripeTestMarkerWithDevelopmentSeederConditions_IsRejected()
    {
        var values = ValidValues();
        values["DevelopmentSeed:DemoPassword"] = "configured";

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Development"));
    }

    [Fact]
    public void TestModeExpectationMustBeExplicit()
    {
        var values = ValidValues();
        values.Remove("Billing:StripeTestModeExpected");

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Theory]
    [InlineData("Billing:StripeSecretKey")]
    [InlineData("Billing:StripeWebhookSigningSecret")]
    [InlineData("Billing:StripeApiVersion")]
    [InlineData("Billing:PlanPriceIds:Starter")]
    [InlineData("Billing:PlanPriceIds:Professional")]
    public void MissingStripeConfiguration_IsRejected(string missingKey)
    {
        var values = ValidValues();
        values.Remove(missingKey);

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    [Fact]
    public void FalseTestModeExpectation_IsRejected()
    {
        var values = ValidValues();
        values["Billing:StripeTestModeExpected"] = "false";

        Assert.Throws<InvalidOperationException>(() =>
            StripeTestEnvironmentGuard.ValidateAndCreate(
                BuildConfiguration(values),
                "Production"));
    }

    private static StripeTestEnvironmentGuard CreateGuard() =>
        StripeTestEnvironmentGuard.ValidateAndCreate(
            BuildConfiguration(ValidValues()),
            "Production");

    private static Dictionary<string, string?> ValidValues() => new()
    {
        ["StayFlow:EnvironmentIdentity"] = "StripeTest",
        ["StayFlow:StripeTest:Database:ApprovedHost"] = ApprovedHost,
        ["StayFlow:StripeTest:Database:ApprovedDatabase"] = ApprovedDatabase,
        ["ConnectionStrings:DefaultConnection"] = $"Host={ApprovedHost};Database={ApprovedDatabase};Username=test",
        ["Billing:Provider"] = "Stripe",
        ["Billing:StripeTestModeExpected"] = "true",
        ["Billing:StripeSecretKey"] = "sk_test_placeholder",
        ["Billing:StripeWebhookSigningSecret"] = "whsec_test_placeholder",
        ["Billing:StripeApiVersion"] = "2024-06-20",
        ["Billing:PlanPriceIds:Starter"] = "price_test_starter",
        ["Billing:PlanPriceIds:Professional"] = "price_test_professional",
        ["Billing:UsageBilling:Enabled"] = "false",
        ["Billing:UsageBilling:Worker:Enabled"] = "false",
        ["Billing:UsageBilling:OpenPeriodInvoiceSafetyVerified"] = "false"
    };

    private static IConfiguration BuildConfiguration(
        params (string Key, string? Value)[] values) =>
        BuildConfiguration(values.ToDictionary(item => item.Key, item => item.Value));

    private static IConfiguration BuildConfiguration(
        IDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
