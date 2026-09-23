using StayFlow.Api.DTOs.AIProvider;

namespace StayFlow.Api.Tests;

public sealed class AITokenAggregationRuntimeTests
{
    [Fact]
    public void AddKnownUsage_SumsKnownCallsIntoCompleteAggregate()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.GuestMessage, Guid.NewGuid());

        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(100, 50, 150));
        aggregation.AddKnownUsage(new ProviderCallId("call-2"), new AiTokenUsage(75, 25, 100));

        Assert.Equal(AiTokenUsageCompleteness.Complete, aggregation.Completeness);
        Assert.Equal(new AiTokenUsage(175, 75, 250), aggregation.Total);
        Assert.Equal(2, aggregation.ProviderCallCount);
    }

    [Fact]
    public void AddUnknownUsage_WithoutKnownValues_LeavesAggregateUnknown()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());

        aggregation.AddUnknownUsage(new ProviderCallId("call-1"));

        Assert.Equal(AiTokenUsageCompleteness.Unknown, aggregation.Completeness);
        Assert.Null(aggregation.Total);
    }

    [Fact]
    public void ZeroObservedCalls_AreDistinguishableFromAllUnknownObservedCalls()
    {
        var zeroObserved = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());
        var allUnknown = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());

        allUnknown.AddUnknownUsage(new ProviderCallId("call-1"));

        Assert.Equal(0, zeroObserved.ProviderCallCount);
        Assert.Equal(AiTokenUsageCompleteness.Unknown, zeroObserved.Completeness);
        Assert.Null(zeroObserved.Total);

        Assert.Equal(1, allUnknown.ProviderCallCount);
        Assert.Equal(AiTokenUsageCompleteness.Unknown, allUnknown.Completeness);
        Assert.Null(allUnknown.Total);
        Assert.NotEqual(zeroObserved.ProviderCallCount, allUnknown.ProviderCallCount);
    }

    [Fact]
    public void AddUnknownUsage_AfterKnownUsage_MarksAggregatePartial()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.GuestMessage, Guid.NewGuid());

        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(100, 50, 150));
        aggregation.AddUnknownUsage(new ProviderCallId("call-2"));

        Assert.Equal(AiTokenUsageCompleteness.Partial, aggregation.Completeness);
        Assert.Null(aggregation.Total);
    }

    [Fact]
    public void UnknownReplay_IsIdempotent()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());

        aggregation.AddUnknownUsage(new ProviderCallId("call-1"));
        aggregation.AddUnknownUsage(new ProviderCallId("call-1"));

        Assert.Equal(1, aggregation.ProviderCallCount);
        Assert.Empty(aggregation.KnownUsageByProviderCallId);
        Assert.Equal(AiTokenUsageCompleteness.Unknown, aggregation.Completeness);
    }

    [Fact]
    public void UnknownThenKnown_EnrichesExistingCall()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());

        aggregation.AddUnknownUsage(new ProviderCallId("call-1"));
        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(100, 20, 120));

        Assert.Equal(1, aggregation.ProviderCallCount);
        Assert.Single(aggregation.KnownUsageByProviderCallId);
        Assert.Equal(new AiTokenUsage(100, 20, 120), aggregation.Total);
        Assert.Equal(AiTokenUsageCompleteness.Complete, aggregation.Completeness);
    }

    [Fact]
    public void KnownReplayWithSameUsage_IsIdempotent()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());
        var usage = new AiTokenUsage(100, 20, 120);

        aggregation.AddKnownUsage(new ProviderCallId("call-1"), usage);
        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(100, 20, 120));

        Assert.Equal(1, aggregation.ProviderCallCount);
        Assert.Equal(usage, aggregation.Total);
    }

    [Fact]
    public void KnownThenUnknown_DoesNotDowngrade()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());

        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(100, 20, 120));
        aggregation.AddUnknownUsage(new ProviderCallId("call-1"));

        Assert.Equal(1, aggregation.ProviderCallCount);
        Assert.Equal(new AiTokenUsage(100, 20, 120), aggregation.Total);
        Assert.Equal(AiTokenUsageCompleteness.Complete, aggregation.Completeness);
    }

    [Fact]
    public void KnownReplayWithDifferentUsage_IsRejected()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());
        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(100, 20, 120));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(200, 40, 240)));

        Assert.Contains("call-1", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new AiTokenUsage(100, 20, 120), aggregation.Total);
    }

    [Fact]
    public void DistinctProviderCallIds_AreAggregated()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());

        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(100, 20, 120));
        aggregation.AddKnownUsage(new ProviderCallId("call-2"), new AiTokenUsage(50, 10, 60));

        Assert.Equal(2, aggregation.ProviderCallCount);
        Assert.Equal(new AiTokenUsage(150, 30, 180), aggregation.Total);
        Assert.Equal(AiTokenUsageCompleteness.Complete, aggregation.Completeness);
    }

    [Fact]
    public void OverflowDuringEnrichment_DoesNotMutateAggregate()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.CopilotOperation, Guid.NewGuid());
        aggregation.AddUnknownUsage(new ProviderCallId("call-1"));
        aggregation.AddKnownUsage(new ProviderCallId("call-2"), new AiTokenUsage(long.MaxValue, 0, long.MaxValue));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(1, 0, 1)));

        Assert.Contains("overflowed", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, aggregation.ProviderCallCount);
        Assert.Single(aggregation.UnknownProviderCallIds);
        Assert.Single(aggregation.KnownUsageByProviderCallId);
        Assert.Equal(AiTokenUsageCompleteness.Partial, aggregation.Completeness);
        Assert.Null(aggregation.Total);
    }

    [Fact]
    public void KnownZeroUsage_IsNotTreatedAsUnknown()
    {
        var aggregation = AiTokenUsageAggregation.Empty(OuterOperationType.GuestMessage, Guid.NewGuid());

        aggregation.AddKnownUsage(new ProviderCallId("call-1"), new AiTokenUsage(0, 0, 0));
        aggregation.AddKnownUsage(new ProviderCallId("call-2"), new AiTokenUsage(8, 2, 10));

        Assert.Equal(AiTokenUsageCompleteness.Complete, aggregation.Completeness);
        Assert.Equal(new AiTokenUsage(8, 2, 10), aggregation.Total);
    }
}
