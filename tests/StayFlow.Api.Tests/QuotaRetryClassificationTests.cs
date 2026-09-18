using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class QuotaRetryClassificationTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    public void PostgreSqlConcurrencyFailures_AreRetryable(string sqlState)
    {
        Assert.True(IsRetryable(new PostgresException("concurrency failure", "ERROR", "ERROR", sqlState)));
    }

    [Theory]
    [InlineData("IX_UsageOperations_CompanyId_Metric_PeriodStartUtc_IdempotencyKey")]
    [InlineData("IX_UsageRecords_CompanyId_Metric_PeriodStartUtc")]
    public void QuotaUniqueIndexes_AreRetryable(string constraintName)
    {
        var exception = new DbUpdateException(
            "quota unique race",
            new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: constraintName));

        Assert.True(IsRetryable(exception));
    }

    [Fact]
    public void UnrelatedUniqueIndex_IsNotRetryable()
    {
        var exception = new DbUpdateException(
            "unrelated unique violation",
            new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: "IX_Users_NormalizedEmail"));

        Assert.False(IsRetryable(exception));
    }

    [Fact]
    public void NonRetryableDatabaseFailure_IsNotRetryable()
    {
        Assert.False(IsRetryable(new DbUpdateException("database failure", new InvalidOperationException("failure"))));
    }

    private static bool IsRetryable(Exception exception)
    {
        var method = typeof(SubscriptionEntitlementService).GetMethod(
            "IsRetryableQuotaRace",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [exception])!;
    }
}
