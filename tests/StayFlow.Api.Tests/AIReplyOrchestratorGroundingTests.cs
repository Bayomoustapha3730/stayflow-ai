using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.AI.Context;
using StayFlow.Api.Services.AI.Intent;
using StayFlow.Api.Services.AI.Orchestration;
using StayFlow.Api.Services.AI.Retrieval;
using StayFlow.Api.Services.AI.Safety;
using StayFlow.Api.Services.AI.Validation;

namespace StayFlow.Api.Tests;

public sealed class AIReplyOrchestratorGroundingTests
{
    [Theory]
    [InlineData("known", 1, 1, AiTokenUsageCompleteness.Complete, false)]
    [InlineData("zero", 1, 1, AiTokenUsageCompleteness.Complete, false)]
    [InlineData("unknown", 1, 0, AiTokenUsageCompleteness.Unknown, false)]
    [InlineData("structured-failure", 1, 0, AiTokenUsageCompleteness.Unknown, true)]
    [InlineData("exception", 1, 0, AiTokenUsageCompleteness.Unknown, true)]
    [InlineData("timeout", 1, 0, AiTokenUsageCompleteness.Unknown, true)]
    public async Task OrchestrateAsync_ObservesProviderAttemptBeforeFallback(
        string mode, int callCount, int knownCount, AiTokenUsageCompleteness completeness, bool fallbackExpected)
    {
        var selected = new ConversationContextKnowledgeItem(
            "token-source", "Guest Wi-Fi", "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider { Mode = mode };
        var outerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var logger = new RecordingLogger();
        var telemetryStore = new CapturingTelemetryStore();
        var orchestrator = BuildOrchestrator(context, provider, [selected], logger, telemetryStore);

        var result = await orchestrator.OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.FutureGuestReply,
            OuterOperationType = OuterOperationType.GuestMessage,
            OuterOperationId = outerId
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(provider.LastRequest);
        var snapshot = Assert.IsType<AiTokenUsageSnapshot>(result!.TokenUsage);
        Assert.Equal(OuterOperationType.GuestMessage, snapshot.OuterOperationType);
        Assert.Equal(outerId, snapshot.OuterOperationId);
        Assert.Equal(callCount, snapshot.ProviderCallCount);
        Assert.Equal(knownCount, snapshot.KnownCallCount);
        Assert.Equal(mode == "known" ? 12 : 0, snapshot.InputTokens);
        Assert.Equal(mode == "known" ? 3 : 0, snapshot.OutputTokens);
        Assert.Equal(mode == "known" ? 15 : 0, snapshot.TotalTokens);
        Assert.Equal(completeness, snapshot.Completeness);
        Assert.Equal(fallbackExpected, result.FallbackUsed);
        Assert.Contains(logger.Messages, message => message.Contains(provider.LastRequest.ProviderCallId.Value, StringComparison.Ordinal));
        if (mode is "exception" or "timeout")
        {
            var indeterminate = Assert.Single(telemetryStore.IndeterminateObservations);
            Assert.Equal(provider.LastRequest.ProviderCallId, indeterminate.ProviderCallId);
            Assert.Equal(companyId, indeterminate.CompanyId);
            Assert.Equal(mode == "timeout" ? OpenAIProviderFailureCategories.Cancelled : OpenAIProviderFailureCategories.Unknown,
                indeterminate.FailureCategory);
        }
        else
        {
            var observation = Assert.Single(telemetryStore.Observations);
            Assert.Equal(provider.LastRequest.ProviderCallId, observation.ProviderCallId);
            Assert.Equal(companyId, observation.CompanyId);
            Assert.Equal(OuterOperationType.GuestMessage, observation.OuterOperationType);
            Assert.Equal(outerId, observation.OuterOperationId);
            Assert.Equal(mode == "structured-failure" ? AIProviderOutcome.Failed : AIProviderOutcome.Success, observation.Outcome);
            Assert.Equal(mode switch
            {
                "known" => new AiTokenUsage(12, 3, 15),
                "zero" => new AiTokenUsage(0, 0, 0),
                _ => null
            }, observation.TokenUsage);
            Assert.False(telemetryStore.CancellationToken.CanBeCanceled);
        }
    }

    [Fact]
    public async Task OrchestrateAsync_CallerCancellationObservesAttemptAndRethrows()
    {
        var selected = new ConversationContextKnowledgeItem(
            "token-source", "Guest Wi-Fi", "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        using var cancellation = new CancellationTokenSource();
        var provider = new SpyDevelopmentProvider { Mode = "cancel", OnEnter = cancellation.Cancel };
        var logger = new RecordingLogger();
        var telemetryStore = new CapturingTelemetryStore();
        var outerId = Guid.NewGuid();
        var orchestrator = BuildOrchestrator(context, provider, [selected], logger, telemetryStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.FutureGuestReply,
            OuterOperationType = OuterOperationType.GuestMessage,
            OuterOperationId = outerId
        }, cancellation.Token));

        Assert.NotNull(provider.LastRequest);
        Assert.Contains(logger.Messages, message => message.Contains(provider.LastRequest!.ProviderCallId.Value, StringComparison.Ordinal)
            && message.Contains("UsageKnown=False", StringComparison.Ordinal)
            && message.Contains("ProviderCallCount=1", StringComparison.Ordinal));
        var indeterminate = Assert.Single(telemetryStore.IndeterminateObservations);
        Assert.Equal(provider.LastRequest.ProviderCallId, indeterminate.ProviderCallId);
        Assert.Equal(OuterOperationType.GuestMessage, telemetryStore.Preparations.Single().OuterOperationType);
        Assert.Equal(outerId, telemetryStore.Preparations.Single().OuterOperationId);
        Assert.False(telemetryStore.CancellationToken.CanBeCanceled);
    }

    [Fact]
    public async Task OrchestrateAsync_DeterministicConflictHasZeroProviderCalls()
    {
        var first = new ConversationContextKnowledgeItem(
            "first", "Guest Wi-Fi", "Password: FirstPassword", PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var second = new ConversationContextKnowledgeItem(
            "second", "Guest Wi-Fi", "Password: SecondPassword", PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow, 9, true, ["wifi"], null);
        var context = BuildContext([first, second], [first, second]);
        var provider = new SpyDevelopmentProvider();
        var outerId = Guid.NewGuid();

        var result = await BuildOrchestrator(context, provider, [first, second]).OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.FutureGuestReply,
            OuterOperationType = OuterOperationType.GuestMessage,
            OuterOperationId = outerId
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(provider.LastRequest);
        Assert.Equal(0, result!.TokenUsage!.ProviderCallCount);
        Assert.Equal(0, result.TokenUsage.KnownCallCount);
        Assert.Equal(outerId, result.TokenUsage.OuterOperationId);
        Assert.Equal(AiTokenUsageCompleteness.Unknown, result.TokenUsage.Completeness);
    }

    [Fact]
    public async Task OrchestrateAsync_RetrievalDiagnosticsOnlyNeverInvokesPaidProviderOrTelemetry()
    {
        var selected = new ConversationContextKnowledgeItem(
            "diagnostics-source", "Guest Wi-Fi", "Network: StayFlowGuest",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider();
        var telemetryStore = new CapturingTelemetryStore();
        var result = await BuildOrchestrator(context, provider, [selected], telemetryStore: telemetryStore)
            .OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
            {
                ConversationId = context.ConversationId,
                Operation = AIReplyOperation.SuggestedHostReplies,
                RetrievalDiagnosticsOnly = true
            }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("NotInvoked", result!.Provider);
        Assert.NotNull(result.RetrievalDiagnostics);
        Assert.Null(result.TokenUsage);
        Assert.DoesNotContain(AIReplyOrchestrationStage.ProviderInvoked, result.CompletedStages);
        Assert.Null(provider.LastRequest);
        Assert.Empty(telemetryStore.Observations);
    }

    [Fact]
    public async Task OrchestrateAsync_ForwardsCommercialOuterIdentityAndProviderCallId()
    {
        var selected = new ConversationContextKnowledgeItem(
            "identity-source-1",
            "Guest Wi-Fi",
            "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi"],
            "Approved identity test knowledge");
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider();
        var outerOperationId = Guid.NewGuid();
        var orchestrator = BuildOrchestrator(context, provider, selectedItems: [selected]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.FutureGuestReply,
            OuterOperationType = OuterOperationType.GuestMessage,
            OuterOperationId = outerOperationId
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal(OuterOperationType.GuestMessage, provider.LastRequest!.OuterOperationType);
        Assert.Equal(outerOperationId, provider.LastRequest.OuterOperationId);
        Assert.NotNull(provider.LastRequest.ProviderCallId);
        Assert.NotEqual(outerOperationId.ToString("N"), provider.LastRequest.ProviderCallId.Value);
    }

    [Fact]
    public async Task OrchestrateAsync_PersistsKnownProviderUsageAtCommercialBoundary()
    {
        var companyId = Guid.NewGuid();
        var outerId = Guid.NewGuid();
        var selected = new ConversationContextKnowledgeItem(
            "telemetry-source", "Guest Wi-Fi", "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider { Mode = "known" };
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"orchestrator-telemetry-{Guid.NewGuid():N}")
            .Options;
        var database = new ApplicationDbContext(dbOptions);
        await using (database)
        {
            await SeedTelemetryBoundaryAsync(database, companyId, outerId);
            var store = new AiProviderCallTelemetryStore(dbOptions);
            var result = await BuildOrchestrator(context, provider, [selected], telemetryStore: store)
                .OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
                {
                    ConversationId = context.ConversationId,
                    Operation = AIReplyOperation.FutureGuestReply,
                    OuterOperationType = OuterOperationType.GuestMessage,
                    OuterOperationId = outerId
                }, CancellationToken.None);

            Assert.NotNull(result);
            var saved = await database.AiProviderCallTelemetries.SingleAsync();
            Assert.Equal(provider.LastRequest!.ProviderCallId.Value, saved.ProviderCallId);
            Assert.Equal(companyId, saved.CompanyId);
            Assert.Equal(OuterOperationType.GuestMessage, saved.OuterOperationType);
            Assert.Equal(outerId, saved.OuterOperationId);
            Assert.Equal("Development", saved.Provider);
            Assert.Equal("stayflow-development-deterministic", saved.RequestedModel);
            Assert.Equal("stayflow-development-deterministic", saved.ResolvedModel);
            Assert.True(saved.UsageKnown);
            Assert.Equal(12, saved.InputTokens);
            Assert.Equal(3, saved.OutputTokens);
            Assert.Equal(15, saved.TotalTokens);
            Assert.Equal(AIProviderOutcome.Success, saved.Outcome);
        }
    }

    [Fact]
    public async Task OrchestrateAsync_PreparationFailurePreventsProviderDispatch()
    {
        var selected = new ConversationContextKnowledgeItem(
            "telemetry-best-effort-source", "Guest Wi-Fi", "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider { Mode = "known" };
        var logger = new RecordingLogger();

        await Assert.ThrowsAsync<AiProviderCallPersistenceException>(() => BuildOrchestrator(
                context,
                provider,
                [selected],
                logger,
                new FailingTelemetryStore())
            .OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
            {
                ConversationId = context.ConversationId,
                Operation = AIReplyOperation.FutureGuestReply
            }, CancellationToken.None));

        Assert.Null(provider.LastRequest);
    }

    [Fact]
    public async Task OrchestrateAsync_PersistsKnownUsageBeforeDownstreamValidationFailure()
    {
        var companyId = Guid.NewGuid();
        var outerId = Guid.NewGuid();
        var selected = new ConversationContextKnowledgeItem(
            "telemetry-order-source", "Guest Wi-Fi", "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider { Mode = "known" };
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"orchestrator-telemetry-order-{Guid.NewGuid():N}")
            .Options;
        await using var database = new ApplicationDbContext(dbOptions);
        await SeedTelemetryBoundaryAsync(database, companyId, outerId);
        var store = new AiProviderCallTelemetryStore(dbOptions);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildOrchestrator(
                context,
                provider,
                [selected],
                telemetryStore: store,
                outputValidator: new ThrowingOutputValidator())
            .OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
            {
                ConversationId = context.ConversationId,
                Operation = AIReplyOperation.FutureGuestReply,
                OuterOperationType = OuterOperationType.GuestMessage,
                OuterOperationId = outerId
            }, CancellationToken.None));

        var saved = await database.AiProviderCallTelemetries.SingleAsync();
        Assert.Equal(provider.LastRequest!.ProviderCallId.Value, saved.ProviderCallId);
        Assert.True(saved.UsageKnown);
        Assert.Equal(12, saved.InputTokens);
        Assert.Equal(3, saved.OutputTokens);
        Assert.Equal(15, saved.TotalTokens);
    }

    [Fact]
    public async Task OrchestrateAsync_PersistsFailedProviderAttemptBeforeFallback()
    {
        var companyId = Guid.NewGuid();
        var outerId = Guid.NewGuid();
        var selected = new ConversationContextKnowledgeItem(
            "telemetry-failure-source", "Guest Wi-Fi", "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider { Mode = "exception" };
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"orchestrator-telemetry-failure-{Guid.NewGuid():N}")
            .Options;
        var database = new ApplicationDbContext(dbOptions);
        await using (database)
        {
            await SeedTelemetryBoundaryAsync(database, companyId, outerId);
            var store = new AiProviderCallTelemetryStore(dbOptions);
            var result = await BuildOrchestrator(context, provider, [selected], telemetryStore: store)
                .OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
                {
                    ConversationId = context.ConversationId,
                    Operation = AIReplyOperation.FutureGuestReply,
                    OuterOperationType = OuterOperationType.GuestMessage,
                    OuterOperationId = outerId
                }, CancellationToken.None);

            Assert.NotNull(result);
            var saved = await database.AiProviderCallTelemetries.SingleAsync();
            Assert.Equal(provider.LastRequest!.ProviderCallId.Value, saved.ProviderCallId);
            Assert.Equal(AiProviderCallLifecycleState.Indeterminate, saved.LifecycleState);
            Assert.Null(saved.Outcome);
            Assert.Equal(OpenAIProviderFailureCategories.Unknown, saved.FailureCategory);
            Assert.False(saved.UsageKnown);
            Assert.Null(saved.InputTokens);
            Assert.Null(saved.OutputTokens);
            Assert.Null(saved.TotalTokens);
            Assert.True(result!.FallbackUsed);
        }
    }

    [Theory]
    [InlineData(CopilotOperationType.CopilotSuggestion, AIReplyOperation.SuggestedHostReplies)]
    [InlineData(CopilotOperationType.CopilotGeneratedReply, AIReplyOperation.GeneratedHostReply)]
    [InlineData(CopilotOperationType.WorkspaceDraft, AIReplyOperation.GeneratedHostReply)]
    public async Task OrchestrateAsync_PersistsCopilotProviderCallForCommercialOperation(
        CopilotOperationType copilotOperationType,
        AIReplyOperation replyOperation)
    {
        var companyId = Guid.NewGuid();
        var outerId = Guid.NewGuid();
        var selected = new ConversationContextKnowledgeItem(
            "copilot-telemetry-source", "Guest Wi-Fi", "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi, DateTimeOffset.UtcNow, 10, true, ["wifi"], null);
        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider { Mode = "known" };
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"copilot-orchestrator-telemetry-{Guid.NewGuid():N}")
            .Options;
        var database = new ApplicationDbContext(dbOptions);
        await using (database)
        {
            var conversationId = Guid.NewGuid();
            var actorUserId = Guid.NewGuid();
            database.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Telemetry test company",
                Slug = $"telemetry-{companyId:N}",
                NormalizedSlug = $"TELEMETRY-{companyId:N}",
                Status = "Active",
                Email = "telemetry@example.test",
                PhoneNumber = "0000000000",
                CountryCode = "US",
                TimeZone = "UTC"
            });
            database.CopilotOperations.Add(new CopilotOperation
            {
                Id = outerId,
                CompanyId = companyId,
                ConversationId = conversationId,
                ActorUserId = actorUserId,
                OperationType = copilotOperationType
            });
            await database.SaveChangesAsync();

            var store = new AiProviderCallTelemetryStore(dbOptions);
            await BuildOrchestrator(context, provider, [selected], telemetryStore: store)
                .OrchestrateAsync(companyId, new AIReplyOrchestrationRequest
                {
                    ConversationId = context.ConversationId,
                    Operation = replyOperation,
                    OuterOperationType = OuterOperationType.CopilotOperation,
                    OuterOperationId = outerId
                }, CancellationToken.None);

            var saved = await database.AiProviderCallTelemetries.SingleAsync();
            Assert.Equal(provider.LastRequest!.ProviderCallId.Value, saved.ProviderCallId);
            Assert.Equal(companyId, saved.CompanyId);
            Assert.Equal(OuterOperationType.CopilotOperation, saved.OuterOperationType);
            Assert.Equal(outerId, saved.OuterOperationId);
            Assert.True(saved.UsageKnown);
            Assert.Equal(12, saved.InputTokens);
            Assert.Equal(3, saved.OutputTokens);
            Assert.Equal(15, saved.TotalTokens);
        }
    }

    [Fact]
    public async Task OrchestrateAsync_WiFiReply_UsesSelectedApprovedKnowledgeAndSource()
    {
        var selected = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi", "network"],
            "Guest wireless details");

        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider();
        var orchestrator = BuildOrchestrator(context, provider, selectedItems: [selected]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.GeneratedHostReply,
            RequestedTone = "professional"
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.FallbackUsed);
        Assert.False(result.RequiresHumanReview);
        Assert.Contains("StayFlowGuest", result.Output);
        Assert.Contains("DemoStay2026", result.Output);
        Assert.DoesNotContain("information is available", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Sources, source => source.SourceType == ConversationContextSourceType.PropertyKnowledge && source.SourceId == "wifi-source-1");

        Assert.NotNull(provider.LastRequest);
        Assert.Equal("WiFi", provider.LastRequest!.DetectedIntent);
        Assert.Contains(provider.LastRequest.SelectedKnowledgeItems, item => item.SourceId == "wifi-source-1");
    }

    [Fact]
    public async Task OrchestrateAsync_ConflictingWiFiPasswords_RequiresHumanReview()
    {
        var first = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi"],
            null);

        var second = new ConversationContextKnowledgeItem(
            "wifi-source-2",
            "Guest Wi-Fi Backup",
            "Network: StayFlowGuest\nPassword: DifferentPassword",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            9,
            true,
            ["wifi"],
            null);

        var context = BuildContext([first, second], [first, second]);
        var orchestrator = BuildOrchestrator(context, new SpyDevelopmentProvider(), selectedItems: [first, second]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.GeneratedHostReply
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanReview);
        Assert.Contains("Conflicting approved Wi-Fi information was found", result.Output);
        Assert.DoesNotContain("DifferentPassword", result.Output);
        Assert.Contains(result.Warnings, warning => warning.Code == "ConflictingApprovedKnowledge");
    }

    [Fact]
    public async Task OrchestrateAsync_IdenticalDuplicateWiFiValues_DoNotTriggerConflict()
    {
        var first = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi"],
            null);

        var second = new ConversationContextKnowledgeItem(
            "wifi-source-2",
            "Guest Wi-Fi Duplicate",
            "Password: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            9,
            true,
            ["wifi"],
            null);

        var context = BuildContext([first, second], [first, second]);
        var orchestrator = BuildOrchestrator(context, new SpyDevelopmentProvider(), selectedItems: [first, second]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.GeneratedHostReply
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanReview);
        Assert.Contains("DemoStay2026", result.Output);
        Assert.DoesNotContain("Conflicting approved Wi-Fi information was found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrchestrateAsync_FutureGuestReply_WithGroundedContext_DoesNotForceHumanReview()
    {
        var selected = new ConversationContextKnowledgeItem(
            "wifi-source-1",
            "Guest Wi-Fi",
            "Network: StayFlowGuest\nPassword: DemoStay2026",
            PropertyKnowledgeCategory.WiFi,
            DateTimeOffset.UtcNow,
            10,
            true,
            ["wifi", "network"],
            "Guest wireless details");

        var context = BuildContext([selected], [selected]);
        var provider = new SpyDevelopmentProvider();
        var orchestrator = BuildOrchestrator(context, provider, selectedItems: [selected]);

        var result = await orchestrator.OrchestrateAsync(Guid.NewGuid(), new AIReplyOrchestrationRequest
        {
            ConversationId = context.ConversationId,
            Operation = AIReplyOperation.FutureGuestReply,
            RequestedTone = "friendly"
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AIReplyOperation.FutureGuestReply, result!.Operation);
        Assert.False(result.RequiresHumanReview);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == "FutureGuestReplyNotEnabled");
        Assert.NotNull(result.Output);
    }

    private static AIReplyOrchestrator BuildOrchestrator(
        ConversationContext context,
        SpyDevelopmentProvider provider,
        IReadOnlyCollection<ConversationContextKnowledgeItem> selectedItems,
        ILogger<AIReplyOrchestrator>? logger = null,
        IAiProviderCallTelemetryStore? telemetryStore = null,
        IAIReplyOutputValidator? outputValidator = null)
    {
        IAIProvider accountedProvider = telemetryStore is null
            ? provider
            : new AccountingAIProvider(
                provider,
                "Development",
                "stayflow-development-deterministic",
                telemetryStore,
                TimeProvider.System,
                NullLogger<AccountingAIProvider>.Instance);

        return new AIReplyOrchestrator(
            new FakeConversationContextBuilder(context),
            new FakeRanker(selectedItems),
            new AIPromptBuilder(Options.Create(new AIPromptOptions())),
            accountedProvider,
            outputValidator ?? new AIReplyOutputValidator(),
            new AIReplySafetyEvaluator(),
            new ContextConfidenceEvaluator(),
            new AIReplyFallbackProvider(),
            new NoOpReservationPaymentGroundingService(),
            Options.Create(new AIReplyOrchestratorOptions
            {
                EnableFallback = true,
                MaximumSelectedKnowledgeItems = 5,
                MaximumSelectedKnowledgeCharacters = 10000,
                ProviderTimeoutSeconds = 10
            }),
            logger ?? NullLogger<AIReplyOrchestrator>.Instance);
    }

    private static async Task SeedTelemetryBoundaryAsync(
        ApplicationDbContext database,
        Guid companyId,
        Guid outerId)
    {
        database.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Telemetry test company",
            Slug = $"telemetry-{companyId:N}",
            NormalizedSlug = $"TELEMETRY-{companyId:N}",
            Status = "Active",
            Email = "telemetry@example.test",
            PhoneNumber = "0000000000",
            CountryCode = "US",
            TimeZone = "UTC"
        });
        database.ConversationMessages.Add(new ConversationMessage
        {
            Id = outerId,
            CompanyId = companyId
        });
        await database.SaveChangesAsync();
    }

    private static ConversationContext BuildContext(
        IReadOnlyCollection<ConversationContextKnowledgeItem> approvedKnowledge,
        IReadOnlyCollection<ConversationContextKnowledgeItem> sourceKnowledge)
    {
        var conversationId = Guid.NewGuid();
        var sourceList = new List<ConversationContextSource>
        {
            new(
                ConversationContextSourceType.Conversation,
                null,
                "Conversation",
                null,
                DateTimeOffset.UtcNow,
                "Conversation metadata and visible message history.",
                true),
            new(
                ConversationContextSourceType.Property,
                null,
                "Demo Property",
                "Property",
                DateTimeOffset.UtcNow,
                "Property details are linked to this conversation.",
                true)
        };

        sourceList.AddRange(sourceKnowledge.Select(item => new ConversationContextSource(
            ConversationContextSourceType.PropertyKnowledge,
            item.SourceId,
            item.Title,
            item.Category.ToString(),
            item.LastUpdated,
            "Approved property knowledge available for AI grounding.",
            true)));

        return new ConversationContext(
            conversationId,
            Guid.NewGuid(),
            "Open",
            "Web",
            "Guest question",
            false,
            false,
            "Host",
            "Guest",
            "guest@example.com",
            Guid.NewGuid(),
            "Demo Property",
            Guid.NewGuid(),
            "CONF-123",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 4),
            "Confirmed",
            [
                new ConversationContextVisibleMessage(
                    "m1",
                    "Guest",
                    DateTimeOffset.UtcNow,
                    "What is the Wi-Fi password?")
            ],
            approvedKnowledge,
            sourceList,
            [],
            false,
            DateTimeOffset.UtcNow);
    }

    private sealed class FakeConversationContextBuilder(ConversationContext context) : IConversationContextBuilder
    {
        public Task<ConversationContext?> BuildAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult<ConversationContext?>(context);
    }

    private sealed class FakeIntentDetector : IGuestIntentDetector
    {
        public GuestIntentResult Detect(ConversationContext context)
            => new(GuestIntent.WiFi, 0.92, ["wifi", "network"], false, "deterministic");
    }

    private sealed class FakeRanker(IReadOnlyCollection<ConversationContextKnowledgeItem> selectedItems) : IPropertyKnowledgeRanker
    {
        public KnowledgeRetrievalResult Rank(
            ConversationContext context,
            GuestIntentResult intent,
            string latestGuestMessage,
            int maxSelectedItems,
            int maxSelectedCharacters)
        {
            var ranked = selectedItems
                .Select((item, index) => new KnowledgeRetrievalCandidate(
                    item.SourceId,
                    item.Category,
                    100,
                    0.95,
                    ["SelectedInTest"],
                    index + 1,
                    item))
                .ToList();

            return new KnowledgeRetrievalResult(
                intent,
                ranked,
                ranked,
                92,
                KnowledgeConfidenceLevel.High,
                KnowledgeRetrievalReasonCode.StrongKeywordMatch,
                true,
                false,
                false,
                false,
                [],
                ["Selected in test ranker."]);
        }
    }

    private sealed class SpyDevelopmentProvider : IAIProvider
    {
        private readonly DevelopmentAIProvider inner = new();

        public AIProviderRequest? LastRequest { get; private set; }
        public string? Mode { get; init; }
        public Action? OnEnter { get; init; }

        public async Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            OnEnter?.Invoke();
            if (Mode == "cancel")
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (Mode == "timeout")
            {
                throw new OperationCanceledException();
            }

            if (Mode == "exception")
            {
                throw new InvalidOperationException("provider failed");
            }

            if (Mode == "structured-failure")
            {
                return new AIProviderResult
                {
                    Outcome = AIProviderOutcome.Failed,
                    ProviderName = "Synthetic",
                    FailureCategory = "SyntheticFailure",
                    ProviderCallId = request.ProviderCallId,
                    OuterOperationType = request.OuterOperationType,
                    OuterOperationId = request.OuterOperationId
                };
            }

            var result = await inner.GenerateAsync(request, cancellationToken);
            if (Mode is "known" or "zero")
            {
                return AIProviderResult.Success(result.ResponseText!, result.ProviderName, result.ModelName,
                    result.RequestId, result.DurationMs, Mode == "zero" ? new AiTokenUsage(0, 0, 0) : new AiTokenUsage(12, 3, 15),
                    request.ProviderCallId, request.OuterOperationType, request.OuterOperationId);
            }

            return result;
        }
    }

    private sealed class FailingTelemetryStore : IAiProviderCallTelemetryStore
    {
        public Task PrepareAsync(AiProviderCallPreparation preparation, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("telemetry store unavailable"));

        public Task<bool> TryStartDispatchAsync(ProviderCallId providerCallId, Guid companyId, DateTimeOffset dispatchStartedAt, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task RecordAsync(AiProviderCallObservation observation, CancellationToken cancellationToken)
        {
            return Task.FromException(new InvalidOperationException("telemetry store unavailable"));
        }

        public Task RecordIndeterminateAsync(AiProviderCallIndeterminateObservation observation, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class CapturingTelemetryStore : IAiProviderCallTelemetryStore
    {
        public List<AiProviderCallPreparation> Preparations { get; } = [];
        public List<AiProviderCallObservation> Observations { get; } = [];
        public List<AiProviderCallIndeterminateObservation> IndeterminateObservations { get; } = [];
        public CancellationToken CancellationToken { get; private set; }

        public Task PrepareAsync(AiProviderCallPreparation preparation, CancellationToken cancellationToken)
        {
            Preparations.Add(preparation);
            return Task.CompletedTask;
        }

        public Task<bool> TryStartDispatchAsync(ProviderCallId providerCallId, Guid companyId, DateTimeOffset dispatchStartedAt, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task RecordAsync(AiProviderCallObservation observation, CancellationToken cancellationToken)
        {
            Observations.Add(observation);
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task RecordIndeterminateAsync(AiProviderCallIndeterminateObservation observation, CancellationToken cancellationToken)
        {
            IndeterminateObservations.Add(observation);
            CancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingOutputValidator : IAIReplyOutputValidator
    {
        public AIReplyValidationResult Validate(
            AIReplyOperation operation,
            string? output,
            IReadOnlyCollection<string> suggestions,
            int maxOutputCharacters,
            int expectedSuggestionCount,
            bool contextIncomplete)
        {
            throw new InvalidOperationException("simulated downstream validation failure");
        }
    }

    private sealed class RecordingLogger : ILogger<AIReplyOrchestrator>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
