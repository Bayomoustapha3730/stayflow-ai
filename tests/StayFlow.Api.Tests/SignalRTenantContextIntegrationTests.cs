using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;

namespace StayFlow.Api.Tests;

public sealed class SignalRTenantContextIntegrationTests : IClassFixture<SignalRTestAppFactory>
{
    internal static readonly Guid CompanyA = SeedData.DemoCompanyId;
    internal static readonly Guid CompanyB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid UserA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid UserB = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    internal static readonly Guid ConversationA = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    internal static readonly Guid ConversationB = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    internal static readonly Guid GuestA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    internal static readonly Guid GuestB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private readonly SignalRTestAppFactory _factory;

    public SignalRTenantContextIntegrationTests(SignalRTestAppFactory factory)
    {
        _factory = factory;
        _factory.EnsureSeeded();
    }

    [Fact]
    public async Task Negotiate_WithValidQueryToken_ReturnsOk()
    {
        using var client = _factory.CreateClient();
        var token = CreateJwtToken(CompanyA, UserA, ["conversations.read"]);

        using var response = await client.PostAsync(
            $"/hubs/conversations/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}",
            new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(payload.RootElement.GetProperty("connectionToken").GetString()));
        Assert.Contains(
            payload.RootElement.GetProperty("availableTransports").EnumerateArray(),
            transport => transport.GetProperty("transport").GetString() == "LongPolling");
    }

    [Fact]
    public async Task LongPolling_GetAndPost_WithValidQueryToken_ReturnOk()
    {
        using var client = _factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var token = CreateJwtToken(CompanyA, UserA, ["conversations.read"]);

        var connectionToken = await NegotiateConnectionTokenAsync(client, token);
        var id = Uri.EscapeDataString(connectionToken);

        using var getResponse = await client.GetAsync($"/hubs/conversations?id={id}&access_token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var postResponse = await client.PostAsync(
            $"/hubs/conversations?id={id}&access_token={Uri.EscapeDataString(token)}",
            new StringContent("{\"protocol\":\"json\",\"version\":1}\u001e", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
    }

    [Fact]
    public async Task Negotiate_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync("/hubs/conversations/negotiate?negotiateVersion=1", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiate_WithInvalidToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(
            "/hubs/conversations/negotiate?negotiateVersion=1&access_token=not-a-jwt",
            new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task QueryToken_IsIgnored_OutsideHubPath()
    {
        using var client = _factory.CreateClient();
        var token = CreateJwtToken(CompanyA, UserA, ["auth.me"]);

        using var response = await client.GetAsync($"/auth/me?access_token={Uri.EscapeDataString(token)}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task JoinConversation_CrossTenantConversation_IsRejected()
    {
        EnsureConversationExists(CompanyA, GuestA, ConversationA);
        EnsureConversationExists(CompanyB, GuestB, ConversationB);

        var token = CreateJwtToken(CompanyA, UserA, ["conversations.read"]);
        await using var connection = CreateLongPollingHubConnection(token);

        await connection.StartAsync();
        var exception = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinConversation", ConversationB));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JoinConversation_SameTenantConversation_Succeeds()
    {
        EnsureConversationExists(CompanyA, GuestA, ConversationA);

        var token = CreateJwtToken(CompanyA, UserA, ["conversations.read"]);
        await using var connection = CreateLongPollingHubConnection(token);

        await connection.StartAsync();
        await connection.InvokeAsync("JoinConversation", ConversationA);
    }

    [Fact]
    public async Task TenantContext_ResolvesAcrossRepeatedLongPollingInvocations()
    {
        EnsureConversationExists(CompanyA, GuestA, ConversationA);

        var token = CreateJwtToken(CompanyA, UserA, ["conversations.read"]);
        await using var connection = CreateLongPollingHubConnection(token);

        await connection.StartAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await connection.InvokeAsync("JoinConversation", ConversationA);
            await connection.InvokeAsync("LeaveConversation", ConversationA);
        }
    }

    [Fact]
    public async Task TenantContext_IsNotSharedAcrossConcurrentConnections()
    {
        EnsureConversationExists(CompanyA, GuestA, ConversationA);
        EnsureConversationExists(CompanyB, GuestB, ConversationB);

        var tokenA = CreateJwtToken(CompanyA, UserA, ["conversations.read"]);
        var tokenB = CreateJwtToken(CompanyB, UserB, ["conversations.read"]);
        await using var connectionA = CreateLongPollingHubConnection(tokenA);
        await using var connectionB = CreateLongPollingHubConnection(tokenB);

        await Task.WhenAll(connectionA.StartAsync(), connectionB.StartAsync());

        await connectionA.InvokeAsync("JoinConversation", ConversationA);
        await connectionB.InvokeAsync("JoinConversation", ConversationB);

        await Assert.ThrowsAsync<HubException>(() => connectionA.InvokeAsync("JoinConversation", ConversationB));
        await Assert.ThrowsAsync<HubException>(() => connectionB.InvokeAsync("JoinConversation", ConversationA));
    }

    [Fact]
    public async Task LongPolling_WithMissingAccessToken_FailsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var token = CreateJwtToken(CompanyA, UserA, ["conversations.read"]);
        var connectionToken = await NegotiateConnectionTokenAsync(client, token);
        var id = Uri.EscapeDataString(connectionToken);

        using var response = await client.GetAsync($"/hubs/conversations?id={id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HubConnection CreateLongPollingHubConnection(string token)
    {
        var hubUri = new Uri(_factory.Server.BaseAddress!, "/hubs/conversations");

        return new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult(token)!;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    private void EnsureConversationExists(Guid companyId, Guid guestId, Guid conversationId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (!dbContext.Guests.Any(guest => guest.Id == guestId))
        {
            dbContext.Guests.Add(new Guest
            {
                Id = guestId,
                CompanyId = companyId,
                FirstName = "Integration",
                LastName = "Guest",
                PreferredLanguage = "en",
                CountryCode = "US",
                IsActive = true
            });
        }

        if (!dbContext.Conversations.Any(conversation => conversation.Id == conversationId))
        {
            dbContext.Conversations.Add(new Conversation
            {
                Id = conversationId,
                CompanyId = companyId,
                GuestId = guestId,
                Channel = GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                LastActivityAt = DateTimeOffset.UtcNow
            });
        }

        dbContext.SaveChanges();
    }

    private static string CreateJwtToken(Guid companyId, Guid userId, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new("company_id", companyId.ToString("D")),
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, "SignalR Integration Test")
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

    private static async Task<string> NegotiateConnectionTokenAsync(HttpClient client, string token)
    {
        using var response = await client.PostAsync(
            $"/hubs/conversations/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}",
            new StringContent(string.Empty));

        response.EnsureSuccessStatusCode();

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("connectionToken").GetString()
            ?? throw new InvalidOperationException("Expected connectionToken in negotiate response.");
    }
}

public sealed class SignalRTestAppFactory : WebApplicationFactory<Program>
{
    public const string JwtIssuer = "StayFlow.Api";
    public const string JwtAudience = "StayFlow.Clients";
    public const string JwtSigningKey = "development-only-secret-key-change-before-production";
    private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
    private static int _seeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused",
                ["DevelopmentSeed:DemoPassword"] = string.Empty
            };

            configBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("signalr-integration", DatabaseRoot));
        });
    }

    public void EnsureSeeded()
    {
        if (Interlocked.Exchange(ref _seeded, 1) == 1)
        {
            return;
        }

        _ = Server;

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        SeedConversations(dbContext);
    }

    private static void SeedConversations(ApplicationDbContext dbContext)
    {
        if (!dbContext.Companies.Any(company => company.Id == SignalRTenantContextIntegrationTests.CompanyB))
        {
            dbContext.Companies.Add(new Company
            {
                Id = SignalRTenantContextIntegrationTests.CompanyB,
                Name = "Tenant B",
                LegalName = "Tenant B LLC",
                Email = "tenantb@example.test",
                PhoneNumber = "+10000000000",
                CountryCode = "US",
                TimeZone = "UTC",
                IsActive = true
            });
        }

        if (!dbContext.Guests.Any(guest => guest.Id == SignalRTenantContextIntegrationTests.GuestA))
        {
            dbContext.Guests.Add(new Guest
            {
                Id = SignalRTenantContextIntegrationTests.GuestA,
                CompanyId = SignalRTenantContextIntegrationTests.CompanyA,
                FirstName = "Tenant",
                LastName = "A Guest",
                PreferredLanguage = "en",
                CountryCode = "US",
                IsActive = true
            });
        }

        if (!dbContext.Guests.Any(guest => guest.Id == SignalRTenantContextIntegrationTests.GuestB))
        {
            dbContext.Guests.Add(new Guest
            {
                Id = SignalRTenantContextIntegrationTests.GuestB,
                CompanyId = SignalRTenantContextIntegrationTests.CompanyB,
                FirstName = "Tenant",
                LastName = "B Guest",
                PreferredLanguage = "en",
                CountryCode = "US",
                IsActive = true
            });
        }

        if (!dbContext.Conversations.Any(conversation => conversation.Id == SignalRTenantContextIntegrationTests.ConversationA))
        {
            dbContext.Conversations.Add(new Conversation
            {
                Id = SignalRTenantContextIntegrationTests.ConversationA,
                CompanyId = SignalRTenantContextIntegrationTests.CompanyA,
                GuestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
                Channel = GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastActivityAt = DateTimeOffset.UtcNow
            });
        }

        if (!dbContext.Conversations.Any(conversation => conversation.Id == SignalRTenantContextIntegrationTests.ConversationB))
        {
            dbContext.Conversations.Add(new Conversation
            {
                Id = SignalRTenantContextIntegrationTests.ConversationB,
                CompanyId = SignalRTenantContextIntegrationTests.CompanyB,
                GuestId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                Channel = GuestChannel.Web,
                Status = ConversationStatus.Open,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                LastActivityAt = DateTimeOffset.UtcNow
            });
        }

        dbContext.SaveChanges();
    }
}
