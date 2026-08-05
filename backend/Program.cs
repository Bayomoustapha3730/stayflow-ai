using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using StayFlow.Api.Extensions;
using StayFlow.Api.Hubs;
using StayFlow.Api.Middleware;
using StayFlow.Api.Configuration;
//using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    var maxBodyBytes = context.Configuration.GetValue<long?>("ProductionHardening:Security:MaximumRequestBodyBytes") ?? 1_048_576;
    options.Limits.MaxRequestBodySize = maxBodyBytes;
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var authPerMinute = builder.Configuration.GetValue<int?>("ProductionHardening:RateLimits:AuthPerMinute") ?? 10;
var guestChatTokenLimit = builder.Configuration.GetValue<int?>("ProductionHardening:RateLimits:GuestChatTokenLimit") ?? 40;
var guestChatTokensPerPeriod = builder.Configuration.GetValue<int?>("ProductionHardening:RateLimits:GuestChatTokensPerPeriod") ?? 20;
var hostApiPerMinute = builder.Configuration.GetValue<int?>("ProductionHardening:RateLimits:HostApiPerMinute") ?? 120;
var aiGenerationPerMinute = builder.Configuration.GetValue<int?>("ProductionHardening:RateLimits:AiGenerationPerMinute") ?? 20;

builder.Services
    .AddOptions<ProductionHardeningOptions>()
    .Bind(builder.Configuration.GetSection(ProductionHardeningOptions.SectionName))
    .Validate(options => options.Correlation.MaximumLength is >= 16 and <= 128,
        "ProductionHardening:Correlation:MaximumLength must be between 16 and 128.")
    .Validate(options => options.Security.MaximumRequestBodyBytes is >= 1024 and <= 20 * 1024 * 1024,
        "ProductionHardening:Security:MaximumRequestBodyBytes must be between 1KB and 20MB.")
    .Validate(options => options.Resilience.TimeoutSeconds is >= 1 and <= 120,
        "ProductionHardening:Resilience:TimeoutSeconds must be between 1 and 120.")
    .ValidateOnStart();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
        }

        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
            ? value?.ToString()
            : null;

        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Type = "https://httpstatuses.com/429",
            Title = "Rate limit exceeded",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Too many requests. Please retry after a short delay.",
            Instance = httpContext.Request.Path
        };

        problem.Extensions["traceId"] = traceId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), cancellationToken);
    };

    options.AddPolicy("public-auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path.Value ?? string.Empty;
        var partitionKey = $"{ip}:{path}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("guest-chat", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var conversationId = context.GetRouteValue("conversationId")?.ToString();
        var reservationId = context.GetRouteValue("reservationId")?.ToString();
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = $"{userId ?? ip}:{conversationId ?? reservationId ?? context.Request.Path.Value}";

        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = guestChatTokenLimit,
            TokensPerPeriod = guestChatTokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(30),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("host-api", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var companyId = context.User.FindFirst("company_id")?.Value;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = $"{companyId ?? "none"}:{userId ?? ip}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = hostApiPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddPolicy("ai-generation", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var companyId = context.User.FindFirst("company_id")?.Value;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var conversationId = context.GetRouteValue("conversationId")?.ToString();
        var partitionKey = $"{companyId ?? "none"}:{userId ?? ip}:{conversationId ?? "none"}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = aiGenerationPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.AddFixedWindowLimiter("health", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 120;
        limiterOptions.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("whatsapp-webhook", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.PermitLimit = 120;
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("StayFlowFrontendDevelopment", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            origins =
            [
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174"
            ];
        }

        if (origins.Any(origin => origin == "*"))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must not contain wildcard '*' when credentials are enabled.");
        }

        policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
            .AllowCredentials();
    });
});

builder.Services.AddApplicationDatabase(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddApplicationAuthentication(builder.Configuration);
builder.Services.AddApplicationHealthChecks();

/*
 * Build only after every builder.Services registration
 * has completed.
 */
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseResponseCompression();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseRouting();

app.UseCors("StayFlowFrontendDevelopment");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateAsyncScope();

    await scope.ServiceProvider
        .GetRequiredService<
            StayFlow.Api.Services.IDevelopmentSeedService
        >()
        .SeedAsync(CancellationToken.None);
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<PermissionAuthorizationMiddleware>();

app.MapControllers();
app.MapHub<ConversationHub>("/hubs/conversations")
.RequireAuthorization()
.RequireCors("StayFlowFrontendDevelopment");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalAsync
})
.RequireRateLimiting("health");

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalAsync
})
.RequireRateLimiting("health");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalAsync
})
.RequireRateLimiting("health");

app.Run();

public partial class Program;