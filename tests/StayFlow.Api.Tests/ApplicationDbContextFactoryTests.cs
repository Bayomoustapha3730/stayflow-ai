using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayFlow.Api.Data;

namespace StayFlow.Api.Tests;

[CollectionDefinition("Design-time factory environment", DisableParallelization = true)]
public sealed class DesignTimeFactoryEnvironmentCollection;

[Collection("Design-time factory environment")]
public sealed class ApplicationDbContextFactoryTests
{
    [Theory]
    [InlineData("Host=other.invalid;Database=stayflow_test;Username=test")]
    [InlineData("Host=pg-test.invalid;Database=other_database;Username=test")]
    public void MarkedMismatchedTarget_ThrowsBeforeReturningContext(string connectionString)
    {
        using var environment = new FactoryEnvironment();
        environment.Set("ConnectionStrings__DefaultConnection", connectionString);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext([]));

        Assert.Equal(
            "Stripe test database connection target does not match the approved host and database.",
            exception.Message);
    }

    [Theory]
    [InlineData("StayFlow__StripeTest__Database__ApprovedHost")]
    [InlineData("StayFlow__StripeTest__Database__ApprovedDatabase")]
    public void MarkedMissingAllowlist_FailsClosed(string key)
    {
        using var environment = new FactoryEnvironment();
        environment.Set(key, string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext([]));

        Assert.Contains("requires an approved database host", exception.Message);
    }

    [Fact]
    public void MarkedDevelopmentEnvironment_IsRejected()
    {
        using var environment = new FactoryEnvironment();
        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("DOTNET_ENVIRONMENT", null);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext([]));

        Assert.Contains("cannot be combined", exception.Message);
    }

    [Fact]
    public void ValidMarkedTarget_ConstructsNpgsqlContextWithClosedConnection()
    {
        using var environment = new FactoryEnvironment();
        using var context = new ApplicationDbContextFactory().CreateDbContext([]);

        AssertClosedSyntheticTarget(context);
    }

    [Fact]
    public void UnmarkedDevelopmentEnvironment_PreservesOfflineContextConstruction()
    {
        using var environment = new FactoryEnvironment();
        environment.Set("StayFlow__EnvironmentIdentity", null);
        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("DOTNET_ENVIRONMENT", null);

        using var context = new ApplicationDbContextFactory().CreateDbContext([]);

        AssertClosedSyntheticTarget(context);
    }

    [Fact]
    public void EnvironmentVariableMarker_IsNotIgnoredForCommandLineTarget()
    {
        using var environment = new FactoryEnvironment();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext(
                ["--ConnectionStrings:DefaultConnection",
                    "Host=other.invalid;Database=stayflow_test;Username=test"]));

        Assert.Contains("does not match the approved", exception.Message);
    }

    [Theory]
    [InlineData("ASPNETCORE_StayFlow__EnvironmentIdentity")]
    [InlineData("DOTNET_StayFlow__EnvironmentIdentity")]
    public void PrefixedHostEnvironmentMarker_IsNotIgnored(string markerKey)
    {
        using var environment = new FactoryEnvironment();
        environment.Set("StayFlow__EnvironmentIdentity", null);
        environment.Set(markerKey, "StripeTest");
        environment.Set("ConnectionStrings__DefaultConnection",
            "Host=other.invalid;Database=stayflow_test;Username=test");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext([]));

        Assert.Contains("does not match the approved", exception.Message);
    }

    [Fact]
    public void CommandLineTarget_OverridesUnsafeEnvironmentTargetBeforeValidation()
    {
        using var environment = new FactoryEnvironment();
        environment.Set("ConnectionStrings__DefaultConnection",
            "Host=other.invalid;Database=stayflow_test;Username=test");

        using var context = new ApplicationDbContextFactory().CreateDbContext(
            ["--ConnectionStrings:DefaultConnection",
                "Host=pg-test.invalid;Database=stayflow_test;Username=test"]);

        AssertClosedSyntheticTarget(context);
    }

    [Fact]
    public void DotnetEnvironment_TakesPrecedenceOverAspnetcoreEnvironment()
    {
        using var environment = new FactoryEnvironment();
        environment.Set("ASPNETCORE_ENVIRONMENT", "Development");
        environment.Set("DOTNET_ENVIRONMENT", "Production");

        using var context = new ApplicationDbContextFactory().CreateDbContext([]);

        AssertClosedSyntheticTarget(context);
    }

    [Fact]
    public void DotnetDevelopmentEnvironment_CannotBeHiddenByAspnetcoreEnvironment()
    {
        using var environment = new FactoryEnvironment();
        environment.Set("DOTNET_ENVIRONMENT", "Development");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext([]));

        Assert.Contains("cannot be combined", exception.Message);
    }

    [Fact]
    public void CommandLineDevelopmentEnvironment_TakesPrecedenceOverEnvironmentVariables()
    {
        using var environment = new FactoryEnvironment();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext(["--environment", "Development"]));

        Assert.Contains("cannot be combined", exception.Message);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    public void InvalidExplicitMarker_FailsClosed(string marker)
    {
        using var environment = new FactoryEnvironment();
        environment.Set("StayFlow__EnvironmentIdentity", marker);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ApplicationDbContextFactory().CreateDbContext([]));

        Assert.Contains("must be StripeTest", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidTarget_DoesNotExposeSyntheticPasswordInExceptionsOrDiagnostics(bool malformed)
    {
        const string sentinel = "synthetic-factory-redaction-sentinel";
        using var environment = new FactoryEnvironment();
        environment.Set("ConnectionStrings__DefaultConnection",
            $"Host=other.invalid;Database=stayflow_test;Username=test;Password='{sentinel}'"
            + (malformed ? ";UnknownOption" : string.Empty));
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var errorOutput = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(errorOutput);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new ApplicationDbContextFactory().CreateDbContext([]));

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

    private static void AssertClosedSyntheticTarget(ApplicationDbContext context)
    {
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        var connection = Assert.IsType<NpgsqlConnection>(context.Database.GetDbConnection());
        Assert.Equal(ConnectionState.Closed, connection.State);
        var target = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        Assert.Equal("pg-test.invalid", target.Host);
        Assert.Equal("stayflow_test", target.Database);
    }

    private sealed class FactoryEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> originalValues = new();

        public FactoryEnvironment()
        {
            Set("ASPNETCORE_ENVIRONMENT", "Production");
            Set("DOTNET_ENVIRONMENT", "Production");
            Set("ASPNETCORE_StayFlow__EnvironmentIdentity", null);
            Set("DOTNET_StayFlow__EnvironmentIdentity", null);
            Set("StayFlow__EnvironmentIdentity", "StripeTest");
            Set("StayFlow__StripeTest__Database__ApprovedHost", "pg-test.invalid");
            Set("StayFlow__StripeTest__Database__ApprovedDatabase", "stayflow_test");
            Set("ConnectionStrings__DefaultConnection",
                "Host=pg-test.invalid;Database=stayflow_test;Username=test");
            Set("Billing__Provider", "Stripe");
            Set("Billing__StripeTestModeExpected", "true");
            Set("Billing__StripeSecretKey", "sk_test_placeholder");
            Set("Billing__StripeWebhookSigningSecret", "whsec_test_placeholder");
            Set("Billing__StripeApiVersion", "2024-06-20");
            Set("Billing__PlanPriceIds__Starter", "price_test_starter");
            Set("Billing__PlanPriceIds__Professional", "price_test_professional");
            Set("Billing__UsageBilling__Enabled", "false");
            Set("Billing__UsageBilling__Worker__Enabled", "false");
            Set("Billing__UsageBilling__OpenPeriodInvoiceSafetyVerified", "false");
        }

        public void Set(string key, string? value)
        {
            originalValues.TryAdd(key, Environment.GetEnvironmentVariable(key));
            Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose()
        {
            foreach (var (key, value) in originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
