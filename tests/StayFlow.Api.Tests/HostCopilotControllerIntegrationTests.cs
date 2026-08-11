using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.Data;
using StayFlow.Api.Models;

namespace StayFlow.Api.Tests;

public sealed class HostCopilotControllerIntegrationTests : IClassFixture<SignalRTestAppFactory>
{
    private static readonly Guid HostCopilotFeatureEntitlementId = Guid.Parse("aaaaaaaa-7777-7777-7777-777777777701");
    private static readonly Guid CompanyASubscriptionId = Guid.Parse("aaaaaaaa-8888-8888-8888-888888888801");
    private static readonly Guid CompanyBSubscriptionId = Guid.Parse("aaaaaaaa-8888-8888-8888-888888888802");
    private static readonly Guid CompanyA = SignalRTenantContextIntegrationTests.CompanyA;
    private static readonly Guid CompanyB = SignalRTenantContextIntegrationTests.CompanyB;
    private static readonly Guid UserA = SignalRTenantContextIntegrationTests.UserA;
    private static readonly Guid GuestA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000011");
    private static readonly Guid GuestB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000022");
    private static readonly Guid PropertyA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid PropertyB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid ConversationA = Guid.Parse("dddddddd-1111-1111-1111-111111111111");
    private static readonly Guid ConversationB = Guid.Parse("eeeeeeee-2222-2222-2222-222222222222");
    private static readonly Guid ActionB = Guid.Parse("ffffffff-3333-3333-3333-333333333333");

    private readonly SignalRTestAppFactory _factory;

    public HostCopilotControllerIntegrationTests(SignalRTestAppFactory factory)
    {
        _factory = factory;
        _factory.EnsureSeeded();
        EnsureSeedData();
    }

    [Fact]
    public async Task GetWorkspace_ReturnsOnlyTenantItems()
    {
        using var client = CreateClientWithToken(CompanyA, UserA, ["conversations.read"]);

        using var response = await client.GetAsync("/host/copilot/workspace");
        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());

        var items = payload.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, item => Guid.Parse(item.GetProperty("conversationId").GetString()!) == ConversationA);
        Assert.DoesNotContain(items, item => Guid.Parse(item.GetProperty("conversationId").GetString()!) == ConversationB);
    }

    [Fact]
    public async Task GetWorkspace_WhenNoOpenConversations_ReturnsEmptyWorkspace()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenantConversations = db.Conversations
                .Where(item => item.CompanyId == CompanyA)
                .ToList();

            foreach (var conversation in tenantConversations)
            {
                conversation.Status = ConversationStatus.Closed;
            }

            db.SaveChanges();
        }

        using var client = CreateClientWithToken(CompanyA, UserA, ["conversations.read"]);

        using var response = await client.GetAsync("/host/copilot/workspace");
        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());

        var data = payload.RootElement.GetProperty("data");
        Assert.Equal(0, data.GetProperty("totalOpenItems").GetInt32());
        Assert.Equal(0, data.GetProperty("totalBreachedSlaItems").GetInt32());
        Assert.Empty(data.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GetWorkspace_ConversationWithoutProperty_DoesNotFail()
    {
        var conversationId = Guid.Parse("eeeeeeee-1111-1111-1111-111111111111");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!db.Conversations.Any(item => item.Id == conversationId))
            {
                db.Conversations.Add(new Conversation
                {
                    Id = conversationId,
                    CompanyId = CompanyA,
                    GuestId = GuestA,
                    PropertyId = null,
                    Channel = DTOs.ReservationContext.GuestChannel.Web,
                    Status = ConversationStatus.Open,
                    StartedAt = DateTimeOffset.UtcNow.AddHours(-4),
                    LastActivityAt = DateTimeOffset.UtcNow.AddHours(-3)
                });

                db.ConversationMessages.Add(new ConversationMessage
                {
                    Id = Guid.NewGuid(),
                    CompanyId = CompanyA,
                    ConversationId = conversationId,
                    SenderType = ConversationSenderType.Guest,
                    MessageType = ConversationMessageType.Text,
                    Content = "Hello, please help.",
                    SentAt = DateTimeOffset.UtcNow.AddHours(-3),
                    IsInternal = false
                });

                db.SaveChanges();
            }
        }

        using var client = CreateClientWithToken(CompanyA, UserA, ["conversations.read"]);
        using var response = await client.GetAsync("/host/copilot/workspace");
        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());
        var items = payload.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, item => Guid.Parse(item.GetProperty("conversationId").GetString()!) == conversationId);
    }

    [Fact]
    public async Task GetWorkspace_WithCrossTenantPropertyFilter_ReturnsBadRequest()
    {
        using var client = CreateClientWithToken(CompanyA, UserA, ["conversations.read"]);

        using var response = await client.GetAsync($"/host/copilot/workspace?propertyId={PropertyB:D}");
        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("Property was not found", payload.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendDraft_CrossTenantConversation_ReturnsBadRequest()
    {
        using var client = CreateClientWithToken(CompanyA, UserA, ["conversations.reply"]);

        using var response = await client.PostAsync(
            $"/host/copilot/conversations/{ConversationB:D}/draft/send",
            JsonContent(new { draft = "Hello, we are on it and will update shortly." }));

        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("Conversation was not found", payload.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveAction_CrossTenantAction_ReturnsBadRequest()
    {
        using var client = CreateClientWithToken(CompanyA, UserA, ["conversations.manage"]);

        using var response = await client.PostAsync(
            $"/host/copilot/actions/{ActionB:D}/approve",
            JsonContent(new { decisionNote = "Not in my tenant" }));

        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("Action was not found", payload.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateDraft_UsesDeterministicMode_WhenLlmWordingDisabled()
    {
        using var client = CreateClientWithToken(CompanyA, UserA, ["conversations.reply"]);

        using var response = await client.PostAsync(
            $"/host/copilot/conversations/{ConversationA:D}/draft",
            JsonContent(new { tone = "professional", hostInstruction = "Be concise" }));

        var payload = await ParseJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());

        var data = payload.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("usedDeterministicFallback").GetBoolean());
        Assert.Equal("deterministic", data.GetProperty("generationMode").GetString());
    }

    private HttpClient CreateClientWithToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var client = _factory.CreateClient();
        var token = CreateJwtToken(companyId, userId, permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private void EnsureSeedData()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        EnsureHostCopilotSubscription(db, CompanyA, CompanyASubscriptionId);
        EnsureHostCopilotSubscription(db, CompanyB, CompanyBSubscriptionId);

        if (!db.Properties.Any(property => property.Id == PropertyA))
        {
            db.Properties.Add(new Property
            {
                Id = PropertyA,
                CompanyId = CompanyA,
                Name = "A Property",
                City = "Nairobi",
                CountryCode = "KE",
                AddressLine1 = "Road 1",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            });
        }

        if (!db.Properties.Any(property => property.Id == PropertyB))
        {
            db.Properties.Add(new Property
            {
                Id = PropertyB,
                CompanyId = CompanyB,
                Name = "B Property",
                City = "Mombasa",
                CountryCode = "KE",
                AddressLine1 = "Road 2",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            });
        }

        if (!db.Guests.Any(guest => guest.Id == GuestA))
        {
            db.Guests.Add(new Guest
            {
                Id = GuestA,
                CompanyId = CompanyA,
                FirstName = "Guest",
                LastName = "A",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            });
        }

        if (!db.Guests.Any(guest => guest.Id == GuestB))
        {
            db.Guests.Add(new Guest
            {
                Id = GuestB,
                CompanyId = CompanyB,
                FirstName = "Guest",
                LastName = "B",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            });
        }

        if (!db.Conversations.Any(conversation => conversation.Id == ConversationA))
        {
            db.Conversations.Add(new Conversation
            {
                Id = ConversationA,
                CompanyId = CompanyA,
                GuestId = GuestA,
                PropertyId = PropertyA,
                Channel = DTOs.ReservationContext.GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-40),
                LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });
        }

        if (!db.Conversations.Any(conversation => conversation.Id == ConversationB))
        {
            db.Conversations.Add(new Conversation
            {
                Id = ConversationB,
                CompanyId = CompanyB,
                GuestId = GuestB,
                PropertyId = PropertyB,
                Channel = DTOs.ReservationContext.GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-35),
                LastActivityAt = DateTimeOffset.UtcNow.AddMinutes(-3)
            });
        }

        if (!db.ConversationMessages.Any(message => message.ConversationId == ConversationA && message.SenderType == ConversationSenderType.Guest))
        {
            db.ConversationMessages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyA,
                ConversationId = ConversationA,
                SenderType = ConversationSenderType.Guest,
                MessageType = ConversationMessageType.Text,
                Content = "Hi, the door lock is not working and we are outside.",
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-6),
                IsInternal = false
            });
        }

        if (!db.ConversationMessages.Any(message => message.ConversationId == ConversationB && message.SenderType == ConversationSenderType.Guest))
        {
            db.ConversationMessages.Add(new ConversationMessage
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyB,
                ConversationId = ConversationB,
                SenderType = ConversationSenderType.Guest,
                MessageType = ConversationMessageType.Text,
                Content = "Need towels please.",
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-4),
                IsInternal = false
            });
        }

        if (!db.PendingConciergeActions.Any(action => action.Id == ActionB))
        {
            db.PendingConciergeActions.Add(new PendingConciergeAction
            {
                Id = ActionB,
                CompanyId = CompanyB,
                ConversationId = ConversationB,
                PropertyId = PropertyB,
                ActionType = ConciergeActionType.RequestHousekeeping,
                SerializedNormalizedParameters = "{}",
                Status = PendingConciergeActionStatus.AwaitingHostApproval,
                IdempotencyKey = "action-b",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(2),
                CreatedFromMessageId = Guid.NewGuid()
            });
        }

        db.SaveChanges();
    }

    private static void EnsureHostCopilotSubscription(ApplicationDbContext db, Guid companyId, Guid subscriptionId)
    {
        var plan = db.SubscriptionPlans.FirstOrDefault(item => item.Id == SeedData.FreePlanId)
            ?? db.SubscriptionPlans.FirstOrDefault(item => item.IsActive && item.Name == "Free")
            ?? throw new InvalidOperationException("Expected the seeded Free subscription plan to exist for HostCopilot integration tests.");

        if (!db.PlanEntitlements.Any(item => item.SubscriptionPlanId == plan.Id && item.Key == FeatureKeys.HostCopilot))
        {
            db.PlanEntitlements.Add(new PlanEntitlement
            {
                Id = HostCopilotFeatureEntitlementId,
                SubscriptionPlanId = plan.Id,
                Key = FeatureKeys.HostCopilot,
                IsEnabled = true,
                IsUnlimited = false
            });
        }

        var existingSubscriptions = db.TenantSubscriptions
            .Where(item => item.CompanyId == companyId)
            .ToList();

        var activeSubscription = existingSubscriptions.FirstOrDefault(item =>
            item.Status == SubscriptionStatus.Active.ToStorageValue()
            || item.Status == SubscriptionStatus.Trialing.ToStorageValue()
            || item.Status == SubscriptionStatus.CancelAtPeriodEnd.ToStorageValue()
            || item.Status == SubscriptionStatus.PastDue.ToStorageValue());

        var periodStartUtc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEndUtc = periodStartUtc.AddMonths(1).AddTicks(-1);

        if (activeSubscription is null)
        {
            db.TenantSubscriptions.Add(new TenantSubscription
            {
                Id = subscriptionId,
                CompanyId = companyId,
                SubscriptionPlanId = plan.Id,
                Status = SubscriptionStatus.Active.ToStorageValue(),
                CurrentPeriodStartUtc = periodStartUtc,
                CurrentPeriodEndUtc = periodEndUtc
            });

            return;
        }

        activeSubscription.SubscriptionPlanId = plan.Id;
        activeSubscription.Status = SubscriptionStatus.Active.ToStorageValue();
        activeSubscription.CurrentPeriodStartUtc = periodStartUtc;
        activeSubscription.CurrentPeriodEndUtc = periodEndUtc;
        activeSubscription.CancelAtPeriodEnd = false;
        activeSubscription.EndedAtUtc = null;
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private static StringContent JsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private static string CreateJwtToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new("company_id", companyId.ToString("D")),
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, "Host Copilot Integration Test")
        };

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SignalRTestAppFactory.JwtSigningKey));
        var token = new JwtSecurityToken(
            issuer: SignalRTestAppFactory.JwtIssuer,
            audience: SignalRTestAppFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
