using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Services.AI.Orchestration;

namespace StayFlow.Api.Tests;

public sealed class CopilotCommercialBoundaryIntegrationTests : IClassFixture<CopilotBoundaryTestFactory>
{
    private static readonly Guid CompanyA = SignalRTenantContextIntegrationTests.CompanyA;
    private static readonly Guid UserA = SignalRTenantContextIntegrationTests.UserA;
    private static readonly Guid GuestA = SignalRTenantContextIntegrationTests.GuestA;
    private static readonly Guid ConversationA = SignalRTenantContextIntegrationTests.ConversationA;
    private readonly CopilotBoundaryTestFactory factory;

    public CopilotCommercialBoundaryIntegrationTests(CopilotBoundaryTestFactory factory)
    {
        this.factory = factory;
        factory.EnsureSeeded();
        EnsureBoundaryData();
        EnsureSubscription();
    }

    [Fact]
    public async Task Suggestions_SuccessPersistsOperationAndChargesExactlyOnce()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        var response = await GetSuggestionsAsync(operationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operation = await GetOperationAsync(operationId);
        Assert.NotNull(operation);
        Assert.Equal(CompanyA, operation!.CompanyId);
        Assert.Equal(ConversationA, operation.ConversationId);
        Assert.Equal(UserA, operation.ActorUserId);
        Assert.Equal(CopilotOperationType.CopilotSuggestion, operation.OperationType);
        Assert.Equal(CopilotOperationStatus.Completed, operation.Status);
        Assert.Equal(1, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
        Assert.Equal(1, factory.Recorder.InvocationCount);
    }

    [Fact]
    public async Task Suggestions_ReplayDoesNotChargeAgain()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        var first = await GetSuggestionsAsync(operationId, "correlation-a");
        var usageAfterFirst = await UsageAsync();
        var second = await GetSuggestionsAsync(operationId, "correlation-b");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, await OperationCountAsync(operationId, CopilotOperationType.CopilotSuggestion));
        Assert.Equal(usageAfterFirst, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task Suggestions_ExhaustionBlocksOrchestrator()
    {
        EnsureSubscription();
        await SetRequestLimitAsync(0);
        var operationId = Guid.NewGuid();
        var before = factory.Recorder.InvocationCount;
        var response = await GetSuggestionsAsync(operationId);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(before, factory.Recorder.InvocationCount);
        Assert.Equal(0, await UsageAsync());
        Assert.Null(await GetOperationAsync(operationId));
        Assert.Equal(0, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task Suggestions_ProviderFailureRemainsCharged()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        factory.Recorder.Fail = true;

        var response = await GetSuggestionsAsync(operationId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var operation = await GetOperationAsync(operationId);
        Assert.NotNull(operation);
        Assert.Equal(CopilotOperationStatus.Failed, operation!.Status);
        Assert.Equal(1, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
        Assert.True(factory.Recorder.InvocationCount > 0);
        factory.Recorder.Fail = false;
    }

    [Fact]
    public async Task GeneratedReply_SuccessPersistsOperationAndChargesExactlyOnce()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        var response = await PostGeneratedReplyAsync(operationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operation = await GetOperationAsync(operationId);
        Assert.NotNull(operation);
        Assert.Equal(CompanyA, operation!.CompanyId);
        Assert.Equal(ConversationA, operation.ConversationId);
        Assert.Equal(UserA, operation.ActorUserId);
        Assert.Equal(CopilotOperationType.CopilotGeneratedReply, operation.OperationType);
        Assert.Equal(CopilotOperationStatus.Completed, operation.Status);
        Assert.Equal(1, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task GeneratedReply_ReplayDoesNotChargeAgain()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        var first = await PostGeneratedReplyAsync(operationId);
        var usageAfterFirst = await UsageAsync();
        var second = await PostGeneratedReplyAsync(operationId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(usageAfterFirst, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task GeneratedReply_ExhaustionBlocksOrchestrator()
    {
        EnsureSubscription();
        await SetRequestLimitAsync(0);
        var operationId = Guid.NewGuid();
        var before = factory.Recorder.InvocationCount;

        var response = await PostGeneratedReplyAsync(operationId);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(before, factory.Recorder.InvocationCount);
        Assert.Equal(0, await UsageAsync());
        Assert.Null(await GetOperationAsync(operationId));
        Assert.Equal(0, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task GeneratedReply_ProviderFailureRemainsCharged()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        factory.Recorder.Fail = true;

        var response = await PostGeneratedReplyAsync(operationId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var operation = await GetOperationAsync(operationId);
        Assert.NotNull(operation);
        Assert.Equal(CopilotOperationStatus.Failed, operation!.Status);
        Assert.Equal(1, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
        factory.Recorder.Fail = false;
    }

    [Fact]
    public async Task WorkspaceDraft_SuccessPersistsOperationAndChargesExactlyOnce()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        var response = await PostWorkspaceDraftAsync(operationId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operation = await GetOperationAsync(operationId);
        Assert.NotNull(operation);
        Assert.Equal(CompanyA, operation!.CompanyId);
        Assert.Equal(ConversationA, operation.ConversationId);
        Assert.Equal(UserA, operation.ActorUserId);
        Assert.Equal(CopilotOperationType.WorkspaceDraft, operation.OperationType);
        Assert.Equal(CopilotOperationStatus.Completed, operation.Status);
        Assert.Equal(1, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task WorkspaceDraft_ReplayDoesNotChargeAgain()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        var first = await PostWorkspaceDraftAsync(operationId);
        var usageAfterFirst = await UsageAsync();
        var second = await PostWorkspaceDraftAsync(operationId);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(usageAfterFirst, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task WorkspaceDraft_ExhaustionBlocksOrchestrator()
    {
        EnsureSubscription();
        await SetRequestLimitAsync(0);
        var operationId = Guid.NewGuid();
        var before = factory.Recorder.InvocationCount;

        var response = await PostWorkspaceDraftAsync(operationId);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(before, factory.Recorder.InvocationCount);
        Assert.Equal(0, await UsageAsync());
        Assert.Null(await GetOperationAsync(operationId));
        Assert.Equal(0, await BillingOperationCountAsync(operationId));
    }

    [Fact]
    public async Task WorkspaceDraft_ProviderFailureRemainsCharged()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        factory.Recorder.Fail = true;

        var response = await PostWorkspaceDraftAsync(operationId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var operation = await GetOperationAsync(operationId);
        Assert.NotNull(operation);
        Assert.Equal(CopilotOperationStatus.Failed, operation!.Status);
        Assert.Equal(1, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
        factory.Recorder.Fail = false;
    }

    [Fact]
    public async Task EachBoundary_ExhaustionBlocksOrchestrator()
    {
        EnsureSubscription();
        await SetRequestLimitAsync(0);
        var before = factory.Recorder.InvocationCount;
        var operationIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        var suggestions = await GetSuggestionsAsync(operationIds[0]);
        var generated = await PostGeneratedReplyAsync(operationIds[1]);
        var workspace = await PostWorkspaceDraftAsync(operationIds[2]);

        Assert.Equal(HttpStatusCode.TooManyRequests, suggestions.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, generated.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, workspace.StatusCode);
        Assert.Equal(before, factory.Recorder.InvocationCount);
        Assert.Equal(0, await UsageAsync());
        Assert.Equal(0, await OperationCountAsync(operationIds[0], CopilotOperationType.CopilotSuggestion));
        Assert.Equal(0, await OperationCountAsync(operationIds[1], CopilotOperationType.CopilotGeneratedReply));
        Assert.Equal(0, await OperationCountAsync(operationIds[2], CopilotOperationType.WorkspaceDraft));
        await SetRequestLimitAsync(1000);
    }

    [Fact]
    public async Task AdmittedProviderFailure_RemainsChargedAtGeneratedReplyBoundary()
    {
        EnsureSubscription();
        var operationId = Guid.NewGuid();
        factory.Recorder.Fail = true;

        var response = await PostGeneratedReplyAsync(operationId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, await OperationCountAsync(operationId, CopilotOperationType.CopilotGeneratedReply));
        Assert.Equal(1, await UsageAsync());
        Assert.Equal(1, await BillingOperationCountAsync(operationId));
        Assert.Equal(CopilotOperationStatus.Failed, await OperationStatusAsync(operationId));
        factory.Recorder.Fail = false;
    }

    private async Task<HttpResponseMessage> GetSuggestionsAsync(Guid operationId, string? correlationId = null)
    {
        using var client = CreateClient(["conversations.read"], correlationId);
        return await client.GetAsync($"/copilot/conversations/{ConversationA:D}/suggested-replies?tone=professional&operationId={operationId:D}");
    }

    private async Task<HttpResponseMessage> PostGeneratedReplyAsync(Guid operationId)
    {
        using var client = CreateClient(["conversations.reply"]);
        return await client.PostAsync(
            $"/copilot/conversations/{ConversationA:D}/suggest-reply",
            JsonContent(new { operationId, tone = "professional", guidance = "Be concise" }));
    }

    private async Task<HttpResponseMessage> PostWorkspaceDraftAsync(Guid operationId)
    {
        using var client = CreateClient(["conversations.reply"]);
        return await client.PostAsync(
            $"/host/copilot/conversations/{ConversationA:D}/draft",
            JsonContent(new { operationId, tone = "professional", hostInstruction = "Be concise" }));
    }

    private HttpClient CreateClient(IReadOnlyCollection<string> permissions, string? correlationId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwtToken(CompanyA, UserA, permissions));
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            client.DefaultRequestHeaders.Add("X-Correlation-Id", correlationId);
        }
        return client;
    }

    private void EnsureSubscription()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plan = db.SubscriptionPlans.First(item => item.Name == "Free");
        if (!db.PlanEntitlements.Any(item => item.SubscriptionPlanId == plan.Id && item.Key == FeatureKeys.HostCopilot))
        {
            db.PlanEntitlements.Add(new PlanEntitlement
            {
                Id = Guid.NewGuid(),
                SubscriptionPlanId = plan.Id,
                Key = FeatureKeys.HostCopilot,
                IsEnabled = true,
                IsUnlimited = false
            });
        }
        var entitlement = db.PlanEntitlements.Single(item => item.SubscriptionPlanId == plan.Id && item.Key == UsageMetric.AiRequests.ToQuotaEntitlementKey());
        entitlement.QuotaLimit = 1000;
        entitlement.IsUnlimited = false;
        var subscription = db.TenantSubscriptions.FirstOrDefault(item => item.CompanyId == CompanyA);
        if (subscription is null)
        {
            db.TenantSubscriptions.Add(new TenantSubscription
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyA,
                SubscriptionPlanId = plan.Id,
                Status = SubscriptionStatus.Active.ToStorageValue(),
                CurrentPeriodStartUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                CurrentPeriodEndUtc = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(-1)
            });
        }
        db.UsageRecords.RemoveRange(db.UsageRecords.Where(item => item.CompanyId == CompanyA && item.Metric == UsageMetric.AiRequests.ToStorageValue()));
        db.UsageOperations.RemoveRange(db.UsageOperations.Where(item => item.CompanyId == CompanyA && item.Metric == UsageMetric.AiRequests.ToStorageValue()));
        db.CopilotOperations.RemoveRange(db.CopilotOperations.Where(item => item.CompanyId == CompanyA));
        db.SaveChanges();
    }

    private void EnsureBoundaryData()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        if (!db.Users.Any(item => item.Id == UserA))
        {
            db.Users.Add(new User
            {
                Id = UserA,
                CompanyId = CompanyA,
                FullName = "Boundary Test Host",
                Email = "boundary-host@example.test",
                NormalizedEmail = "BOUNDARY-HOST@EXAMPLE.TEST",
                Role = "Admin",
                PasswordHash = "test-password"
            });
        }

        if (!db.Guests.Any(item => item.Id == GuestA))
        {
            db.Guests.Add(new Guest
            {
                Id = GuestA,
                CompanyId = CompanyA,
                FirstName = "Boundary",
                LastName = "Guest",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            });
        }

        if (!db.Conversations.Any(item => item.Id == ConversationA))
        {
            db.Conversations.Add(new Conversation
            {
                Id = ConversationA,
                CompanyId = CompanyA,
                GuestId = GuestA,
                Channel = DTOs.ReservationContext.GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastActivityAt = DateTimeOffset.UtcNow
            });
        }

        if (!db.ConversationMessages.Any(item => item.CompanyId == CompanyA && item.ConversationId == ConversationA && item.SenderType == ConversationSenderType.Guest && !item.IsInternal))
        {
            db.ConversationMessages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyA,
                ConversationId = ConversationA,
                SenderType = ConversationSenderType.Guest,
                MessageType = ConversationMessageType.Text,
                Content = "Please help with my reservation.",
                SentAt = DateTimeOffset.UtcNow,
                IsInternal = false
            });
        }

        db.SaveChanges();
    }

    private async Task SetRequestLimitAsync(long limit)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var plan = await db.SubscriptionPlans.FirstAsync(item => item.Name == "Free");
        var entitlement = await db.PlanEntitlements.SingleAsync(item => item.SubscriptionPlanId == plan.Id && item.Key == UsageMetric.AiRequests.ToQuotaEntitlementKey());
        entitlement.QuotaLimit = limit;
        await db.SaveChangesAsync();
    }

    private async Task<long> UsageAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var periodStartUtc = await db.TenantSubscriptions
            .Where(item => item.CompanyId == CompanyA && item.Status == SubscriptionStatus.Active.ToStorageValue())
            .Select(item => item.CurrentPeriodStartUtc)
            .SingleAsync();
        return await db.UsageRecords
            .Where(item => item.CompanyId == CompanyA
                && item.Metric == UsageMetric.AiRequests.ToStorageValue()
                && item.PeriodStartUtc == periodStartUtc)
            .Select(item => (long?)item.QuantityUsed)
            .SingleOrDefaultAsync() ?? 0;
    }

    private async Task<int> BillingOperationCountAsync(Guid operationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UsageOperations.CountAsync(item => item.CompanyId == CompanyA && item.Metric == UsageMetric.AiRequests.ToStorageValue() && item.IdempotencyKey == $"ai-request:copilot-operation:{operationId:N}");
    }

    private async Task<int> OperationCountAsync(Guid operationId, CopilotOperationType type)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.CopilotOperations.CountAsync(item => item.Id == operationId && item.OperationType == type);
    }

    private async Task<CopilotOperation?> GetOperationAsync(Guid operationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.CopilotOperations.SingleOrDefaultAsync(item => item.Id == operationId);
    }

    private async Task<CopilotOperationStatus> OperationStatusAsync(Guid operationId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.CopilotOperations.Where(item => item.Id == operationId).Select(item => item.Status).SingleAsync();
    }

    private static StringContent JsonContent(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static string CreateJwtToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("company_id", companyId.ToString()),
            new("user_id", userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "Admin")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SignalRTestAppFactory.JwtSigningKey));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: SignalRTestAppFactory.JwtIssuer,
            audience: SignalRTestAppFactory.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)));
    }
}

public sealed class CopilotBoundaryTestFactory : SignalRTestAppFactory
{
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    public RecordingAIReplyOrchestrator Recorder { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HostCopilot:EnableLlmWording"] = "true"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("copilot-boundary-integration", DatabaseRoot));
            services.RemoveAll<IAIReplyOrchestrator>();
            services.AddScoped<IAIReplyOrchestrator>(_ => Recorder);
        });
    }
}

public sealed class RecordingAIReplyOrchestrator : IAIReplyOrchestrator
{
    public int InvocationCount { get; private set; }
    public bool Fail { get; set; }

    public Task<AIReplyOrchestrationResult?> OrchestrateAsync(Guid companyId, AIReplyOrchestrationRequest request, CancellationToken cancellationToken)
    {
        InvocationCount++;
        if (Fail)
        {
            throw new InvalidOperationException("simulated provider failure");
        }

        return Task.FromResult<AIReplyOrchestrationResult?>(new AIReplyOrchestrationResult
        {
            ConversationId = request.ConversationId,
            Operation = request.Operation,
            Output = "Generated test reply.",
            Suggestions = ["Generated suggestion one", "Generated suggestion two", "Generated suggestion three"],
            Provider = "Test",
            GeneratedAt = DateTimeOffset.UtcNow
        });
    }
}
