using StayFlow.Api.Extensions;
using StayFlow.Api.Hubs;
using StayFlow.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("StayFlowFrontendDevelopment", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    return false;
                }

                if (origin is
                    "http://localhost:5173" or
                    "http://127.0.0.1:5173")
                {
                    return true;
                }

                return Uri.TryCreate(
                           origin,
                           UriKind.Absolute,
                           out var uri)
                       && uri.Scheme == Uri.UriSchemeHttps
                       && uri.Host.EndsWith(
                           ".app.github.dev",
                           StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddApplicationDatabase(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddApplicationAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();

/*
 * Build only after every builder.Services registration
 * has completed.
 */
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.UseCors("StayFlowFrontendDevelopment");

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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<PermissionAuthorizationMiddleware>();

app.MapControllers();
app.MapHub<ConversationHub>("/hubs/conversations")
.RequireCors("StayFlowFrontendDevelopment");
app.MapHealthChecks("/health");

app.Run();