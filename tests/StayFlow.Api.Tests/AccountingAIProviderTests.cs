using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Services;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Tests;

public sealed class AccountingAIProviderTests
{
    [Fact]
    public async Task GenerateAsync_PersistsIdentityAroundSingleDispatch()
    {
        var callId = new ProviderCallId("durable-call");
        var companyId = Guid.NewGuid();
        var request = new AIProviderRequest
        {
            CompanyId = companyId,
            ProviderCallId = callId,
            OuterOperationType = OuterOperationType.GuestMessage,
            OuterOperationId = Guid.NewGuid()
        };
        var inner = new CapturingProvider();
        var store = new CapturingStore();
        var calculator = new CapturingCostCalculator();
        var provider = CreateProvider(inner, store, calculator);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(callId, inner.LastRequest!.ProviderCallId);
        Assert.Equal(callId, store.Preparation!.ProviderCallId);
        Assert.Equal(companyId, store.Preparation.CompanyId);
        Assert.Equal("configured-model", store.Preparation.RequestedModel);
        var observation = Assert.Single(store.Observations);
        Assert.Equal("configured-model", observation.RequestedModel);
        Assert.Equal("provider-resolved-model", observation.ResolvedModel);
        Assert.Equal("provider-request-id", observation.ProviderRequestId);
        Assert.Equal(new AiTokenUsage(2, 1, 3), observation.TokenUsage);
        Assert.Equal(AiProviderCallLifecycleState.Observed, observation.LifecycleState);
        Assert.Equal("configured-model", result.RequestedModel);
        Assert.Equal("provider-resolved-model", result.ResolvedModel);
        Assert.Equal("provider-resolved-model", calculator.LastFacts!.ResolvedModel);
        Assert.Equal(store.DispatchStartedAt, calculator.LastFacts.DispatchStartedAt);
    }

    [Fact]
    public async Task GenerateAsync_PreparationOrClaimFailurePreventsDispatch()
    {
        var inner = new CapturingProvider();
        var preparationFailureStore = new CapturingStore
        {
            PreparationFailure = new InvalidOperationException("prepare failed")
        };
        var preparationProvider = CreateProvider(inner, preparationFailureStore);

        await Assert.ThrowsAsync<AiProviderCallPersistenceException>(() =>
            preparationProvider.GenerateAsync(Request(), CancellationToken.None));

        Assert.Equal(0, inner.CallCount);

        var claimFailureStore = new CapturingStore { DispatchClaimed = false };
        var claimProvider = CreateProvider(inner, claimFailureStore);
        await Assert.ThrowsAsync<AiProviderCallTelemetryConflictException>(() =>
            claimProvider.GenerateAsync(Request(), CancellationToken.None));

        Assert.Equal(0, inner.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_CompletionPersistenceFailureDoesNotRedispatch()
    {
        var inner = new CapturingProvider();
        var store = new CapturingStore
        {
            CompletionFailure = new InvalidOperationException("completion write failed")
        };
        var provider = CreateProvider(inner, store);

        await Assert.ThrowsAsync<AiProviderCallPersistenceException>(() =>
            provider.GenerateAsync(Request(), CancellationToken.None));

        Assert.Equal(1, inner.CallCount);
        Assert.Equal(1, store.DispatchClaimCalls);
        Assert.Equal(1, store.RecordCalls);
    }

    [Fact]
    public async Task GenerateAsync_CostPersistenceFailureDoesNotRedispatchOrReplaceProviderResult()
    {
        var inner = new CapturingProvider();
        var store = new CapturingStore
        {
            CostPersistenceFailure = new InvalidOperationException("cost write failed")
        };
        var provider = CreateProvider(inner, store);

        var result = await provider.GenerateAsync(Request(), CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Equal(1, inner.CallCount);
        Assert.Equal(1, store.DispatchClaimCalls);
        Assert.Equal(1, store.CostPersistenceCalls);
    }

    [Fact]
    public async Task GenerateAsync_ProviderFailurePersistsUnavailableUsageWithoutCost()
    {
        var inner = new CapturingProvider { Failure = new TimeoutException("provider timeout") };
        var store = new CapturingStore();
        var calculator = new CapturingCostCalculator();
        var provider = CreateProvider(inner, store, calculator);

        await Assert.ThrowsAsync<TimeoutException>(() => provider.GenerateAsync(Request(), CancellationToken.None));

        Assert.Equal(1, inner.CallCount);
        Assert.True(calculator.LastFacts!.ProviderUsageUnavailable);
        Assert.Null(Assert.Single(store.Costs).Amount);
        Assert.Equal(AiProviderCostReason.ProviderUsageUnavailable, Assert.Single(store.Costs).Reason);
    }

    [Fact]
    public async Task GenerateAsync_IndeterminateOutcomeWithKnownUsageCanStillBeCalculated()
    {
        var inner = new CapturingProvider
        {
            Result = new AIProviderResult
            {
                Outcome = AIProviderOutcome.Failed,
                ProviderName = "OpenAI",
                ResolvedModel = "provider-resolved-model",
                FailureCategory = OpenAIProviderFailureCategories.Timeout,
                TokenUsage = new AiTokenUsage(2, 1, 900),
                ProviderCallId = new ProviderCallId("indeterminate-call")
            }
        };
        var store = new CapturingStore();
        var calculator = new CapturingCostCalculator();
        var provider = CreateProvider(inner, store, calculator);

        var result = await provider.GenerateAsync(Request(), CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Failed, result.Outcome);
        Assert.Equal(AiProviderCallLifecycleState.Indeterminate, Assert.Single(store.Observations).LifecycleState);
        Assert.False(calculator.LastFacts!.ProviderUsageUnavailable);
        Assert.Equal(2, calculator.LastFacts.InputTokens);
        Assert.Equal(1, calculator.LastFacts.OutputTokens);
        Assert.Equal(900, Assert.Single(store.Observations).TokenUsage!.TotalTokens);
    }

    private static AccountingAIProvider CreateProvider(
        CapturingProvider inner,
        CapturingStore store,
        CapturingCostCalculator? calculator = null) =>
        new(inner, "OpenAI", "configured-model", store, calculator ?? new CapturingCostCalculator(),
            TimeProvider.System, NullLogger<AccountingAIProvider>.Instance);

    private static AIProviderRequest Request() => new()
    {
        CompanyId = Guid.NewGuid()
    };

    private sealed class CapturingProvider : IAIProvider
    {
        public int CallCount { get; private set; }
        public AIProviderRequest? LastRequest { get; private set; }
        public Exception? Failure { get; init; }
        public AIProviderResult? Result { get; init; }

        public Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            if (Failure is not null)
            {
                return Task.FromException<AIProviderResult>(Failure);
            }

            if (Result is not null)
            {
                return Task.FromResult(Result);
            }

            return Task.FromResult(new AIProviderResult
            {
                Outcome = AIProviderOutcome.Success,
                ProviderName = "OpenAI",
                RequestedModel = "configured-model",
                ResolvedModel = "provider-resolved-model",
                ModelName = "provider-resolved-model",
                RequestId = "provider-request-id",
                TokenUsage = new AiTokenUsage(2, 1, 3),
                ProviderCallId = request.ProviderCallId
            });
        }
    }

    private sealed class CapturingStore : IAiProviderCallTelemetryStore
    {
        public AiProviderCallPreparation? Preparation { get; private set; }
        public List<AiProviderCallObservation> Observations { get; } = [];
        public bool DispatchClaimed { get; init; } = true;
        public Exception? PreparationFailure { get; init; }
        public Exception? CompletionFailure { get; init; }
        public Exception? CostPersistenceFailure { get; init; }
        public int DispatchClaimCalls { get; private set; }
        public int RecordCalls { get; private set; }
        public int CostPersistenceCalls { get; private set; }
        public List<AiProviderCostResult> Costs { get; } = [];
        public DateTimeOffset DispatchStartedAt { get; private set; }

        public Task PrepareAsync(AiProviderCallPreparation preparation, CancellationToken cancellationToken)
        {
            if (PreparationFailure is not null)
            {
                return Task.FromException(PreparationFailure);
            }

            Preparation = preparation;
            return Task.CompletedTask;
        }

        public Task<bool> TryStartDispatchAsync(
            ProviderCallId providerCallId,
            Guid companyId,
            DateTimeOffset dispatchStartedAt,
            CancellationToken cancellationToken)
        {
            DispatchClaimCalls++;
            DispatchStartedAt = dispatchStartedAt;
            return Task.FromResult(DispatchClaimed);
        }

        public Task RecordAsync(AiProviderCallObservation observation, CancellationToken cancellationToken)
        {
            RecordCalls++;
            if (CompletionFailure is not null)
            {
                return Task.FromException(CompletionFailure);
            }

            Observations.Add(observation);
            return Task.CompletedTask;
        }

        public Task RecordIndeterminateAsync(AiProviderCallIndeterminateObservation observation, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PersistCostAsync(
            ProviderCallId providerCallId,
            Guid companyId,
            AiProviderCostResult cost,
            CancellationToken cancellationToken)
        {
            CostPersistenceCalls++;
            Costs.Add(cost);
            return CostPersistenceFailure is null
                ? Task.CompletedTask
                : Task.FromException(CostPersistenceFailure);
        }
    }

    private sealed class CapturingCostCalculator : IAiProviderCostCalculator
    {
        public AiProviderCostFacts? LastFacts { get; private set; }

        public Task<AiProviderCostResult> CalculateAsync(AiProviderCostFacts facts, CancellationToken cancellationToken)
        {
            LastFacts = facts;
            return Task.FromResult(facts.ProviderUsageUnavailable
                ? new AiProviderCostResult(AiProviderCostState.Unknown, AiProviderCostReason.ProviderUsageUnavailable)
                : new AiProviderCostResult(AiProviderCostState.Unknown, AiProviderCostReason.NoEffectivePrice));
        }
    }
}
