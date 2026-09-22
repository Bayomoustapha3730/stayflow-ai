using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CopilotOperationAdmissionTests
{
    [Fact]
    public async Task NewOperation_PersistsIdentityAndConsumesOneRequest()
    {
        var harness = await CreateHarnessAsync(limit: 5);

        var result = await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotSuggestion);
        var operation = await harness.Db.CopilotOperations.SingleAsync();

        Assert.False(result.WasReplay);
        Assert.Equal(harness.OperationId, operation.Id);
        Assert.Equal(harness.CompanyId, operation.CompanyId);
        Assert.Equal(harness.ConversationId, operation.ConversationId);
        Assert.Equal(harness.ActorUserId, operation.ActorUserId);
        Assert.Equal(CopilotOperationType.CopilotSuggestion, operation.OperationType);
        Assert.Equal(CopilotOperationStatus.Pending, operation.Status);
        Assert.Equal(1, await harness.UsageAsync());
        Assert.Equal(1, await harness.BillingOperationsAsync());
    }

    [Fact]
    public async Task SameOperationReplay_PreservesIdentityAndUsage()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotSuggestion);
        var before = await harness.UsageAsync();

        var replay = await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotSuggestion);

        Assert.True(replay.WasReplay);
        Assert.Single(await harness.Db.CopilotOperations.ToListAsync());
        Assert.Equal(before, await harness.UsageAsync());
        Assert.Equal(1, await harness.BillingOperationsAsync());
    }

    [Fact]
    public async Task DifferentOperationIds_CreateDistinctOperationsAndConsumeDistinctUnits()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        var secondId = Guid.NewGuid();

        await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotSuggestion);
        await harness.AdmitAsync(secondId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotGeneratedReply);

        Assert.Equal(2, await harness.Db.CopilotOperations.CountAsync());
        Assert.Equal(2, await harness.UsageAsync());
        Assert.Equal(2, await harness.BillingOperationsAsync());
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("conversation")]
    [InlineData("actor")]
    [InlineData("type")]
    public async Task ExistingOperationCannotBeReusedWithDifferentIdentity(string conflict)
    {
        var harness = await CreateHarnessAsync(limit: 5);
        await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotSuggestion);
        var companyId = conflict == "tenant" ? Guid.NewGuid() : harness.CompanyId;
        var conversationId = conflict == "conversation" ? Guid.NewGuid() : harness.ConversationId;
        var actorUserId = conflict == "actor" ? Guid.NewGuid() : harness.ActorUserId;
        var operationType = conflict == "type" ? CopilotOperationType.WorkspaceDraft : CopilotOperationType.CopilotSuggestion;

        await Assert.ThrowsAsync<ConflictException>(() => harness.AdmitAsync(harness.OperationId, conversationId, actorUserId, operationType, companyId));

        var operation = await harness.Db.CopilotOperations.SingleAsync();
        Assert.Equal(harness.CompanyId, operation.CompanyId);
        Assert.Equal(harness.ConversationId, operation.ConversationId);
        Assert.Equal(harness.ActorUserId, operation.ActorUserId);
        Assert.Equal(CopilotOperationType.CopilotSuggestion, operation.OperationType);
        Assert.Equal(1, await harness.UsageAsync());
        Assert.Equal(1, await harness.BillingOperationsAsync());
    }

    [Fact]
    public async Task ExhaustedQuotaDoesNotInvokeProviderOrPersistAdmittedOperation()
    {
        var harness = await CreateHarnessAsync(limit: 0);
        var providerInvocations = 0;

        await Assert.ThrowsAsync<QuotaExceededException>(() => harness.AdmitAsync(
            harness.OperationId,
            harness.ConversationId,
            harness.ActorUserId,
            CopilotOperationType.CopilotSuggestion,
            provider: () => providerInvocations++));

        Assert.Equal(0, providerInvocations);
        Assert.Empty(await harness.Db.CopilotOperations.ToListAsync());
        Assert.Equal(0, await harness.UsageAsync());
        Assert.Equal(0, await harness.BillingOperationsAsync());
    }

    [Fact]
    public async Task AdmissionExistsBeforeProviderAndProviderFailureDoesNotRefund()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        var providerObservedOperation = false;
        var providerInvocations = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.AdmitAsync(
            harness.OperationId,
            harness.ConversationId,
            harness.ActorUserId,
            CopilotOperationType.CopilotGeneratedReply,
            provider: () =>
            {
                providerInvocations++;
                providerObservedOperation = harness.Db.CopilotOperations.Any(operation => operation.Id == harness.OperationId)
                    && harness.Db.UsageOperations.Any(operation => operation.IdempotencyKey == harness.BillingKey);
                throw new InvalidOperationException("simulated provider failure");
            }));
        await harness.Service.MarkCopilotOperationFailedAsync(harness.CompanyId, harness.OperationId, CancellationToken.None);

        var operation = await harness.Db.CopilotOperations.SingleAsync();
        Assert.Equal(1, providerInvocations);
        Assert.True(providerObservedOperation);
        Assert.Equal(CopilotOperationStatus.Failed, operation.Status);
        Assert.Equal(1, await harness.UsageAsync());
        Assert.Equal(1, await harness.BillingOperationsAsync());
    }

    [Fact]
    public async Task FailedOperationReplayDoesNotConsumeAnotherRequest()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.WorkspaceDraft);
        await harness.Service.MarkCopilotOperationFailedAsync(harness.CompanyId, harness.OperationId, CancellationToken.None);
        var before = await harness.UsageAsync();

        var replay = await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.WorkspaceDraft);

        Assert.True(replay.WasReplay);
        Assert.Equal(before, await harness.UsageAsync());
        Assert.Equal(1, await harness.BillingOperationsAsync());
        Assert.Equal(CopilotOperationStatus.Failed, (await harness.Db.CopilotOperations.SingleAsync()).Status);
    }

    [Fact]
    public async Task CorrelationDoesNotParticipateInBillingIdentity()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotSuggestion);

        var replay = await harness.AdmitAsync(harness.OperationId, harness.ConversationId, harness.ActorUserId, CopilotOperationType.CopilotSuggestion);

        Assert.True(replay.WasReplay);
        Assert.Equal($"ai-request:copilot-operation:{harness.OperationId:N}", harness.BillingKey);
        Assert.Equal(1, await harness.BillingOperationsAsync());
    }

    private static async Task<Harness> CreateHarnessAsync(long? limit)
    {
        var companyId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var periodStart = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1).AddTicks(-1);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"copilot-operation-tests-{Guid.NewGuid():N}")
            .Options;
        var tenant = new TestTenantContext(companyId, actorUserId);
        var db = new ApplicationDbContext(options, tenant);
        db.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Copilot Test Company",
            Slug = $"copilot-{companyId:N}",
            NormalizedSlug = $"COPILOT-{companyId:N}",
            Status = "Active",
            Email = $"{companyId:N}@test.local",
            PhoneNumber = "+254700000000",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        db.Users.Add(new User
        {
            Id = actorUserId,
            CompanyId = companyId,
            FullName = "Copilot Host",
            Email = $"{actorUserId:N}@test.local",
            NormalizedEmail = $"{actorUserId:N}@TEST.LOCAL",
            PhoneNumber = "+254700000001",
            Role = "Admin",
            PasswordHash = "test-hash",
            IsActive = true
        });
        db.Guests.Add(new Guest { Id = guestId, CompanyId = companyId, FirstName = "Guest", LastName = "Test", IsActive = true });
        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            CompanyId = companyId,
            GuestId = guestId,
            Channel = DTOs.ReservationContext.GuestChannel.Web,
            Status = ConversationStatus.Open,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow
        });
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = "CopilotTest",
            DisplayName = "Copilot Test",
            Description = "Copilot operation test plan",
            IsActive = true,
            Entitlements =
            [
                new PlanEntitlement
                {
                    Id = Guid.NewGuid(),
                    Key = UsageMetric.AiRequests.ToQuotaEntitlementKey(),
                    IsEnabled = true,
                    QuotaLimit = limit,
                    IsUnlimited = limit is null,
                    Unit = "requests"
                }
            ]
        });
        db.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStart,
            CurrentPeriodEndUtc = periodEnd
        });
        await db.SaveChangesAsync();
        return new Harness(db, new SubscriptionEntitlementService(db, NullLogger<SubscriptionEntitlementService>.Instance), companyId, conversationId, actorUserId, periodStart, Guid.NewGuid());
    }

    private sealed class Harness(
        ApplicationDbContext db,
        SubscriptionEntitlementService service,
        Guid companyId,
        Guid conversationId,
        Guid actorUserId,
        DateTimeOffset periodStart,
        Guid operationId)
    {
        public ApplicationDbContext Db { get; } = db;
        public SubscriptionEntitlementService Service { get; } = service;
        public Guid CompanyId { get; } = companyId;
        public Guid ConversationId { get; } = conversationId;
        public Guid ActorUserId { get; } = actorUserId;
        public Guid OperationId { get; } = operationId;
        public string BillingKey => $"ai-request:copilot-operation:{OperationId:N}";
        public DateTimeOffset PeriodStart { get; } = periodStart;

        public async Task<AIRequestAdmissionResult> AdmitAsync(
            Guid operationId,
            Guid conversationId,
            Guid actorUserId,
            CopilotOperationType operationType,
            Guid? companyId = null,
            Action? provider = null)
        {
            var result = await Service.AdmitCopilotOperationAsync(
                companyId ?? CompanyId,
                operationId,
                conversationId,
                actorUserId,
                operationType,
                CancellationToken.None);
            provider?.Invoke();
            return result;
        }

        public Task<long> UsageAsync() => Db.UsageRecords
            .Where(record => record.CompanyId == CompanyId && record.Metric == UsageMetric.AiRequests.ToStorageValue() && record.PeriodStartUtc == PeriodStart)
            .Select(record => (long?)record.QuantityUsed)
            .SingleOrDefaultAsync()
            .ContinueWith(task => task.Result ?? 0L);

        public Task<int> BillingOperationsAsync() => Db.UsageOperations.CountAsync(operation => operation.CompanyId == CompanyId && operation.Metric == UsageMetric.AiRequests.ToStorageValue() && operation.PeriodStartUtc == PeriodStart);
    }

    private sealed class TestTenantContext(Guid companyId, Guid userId) : ITenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? TenantId => CompanyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => "test-correlation";
        public bool IsAuthenticated => true;
    }
}
