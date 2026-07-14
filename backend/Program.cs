using StayFlow.Api.Extensions;
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

                if (origin is "http://localhost:5173"
                    or "http://127.0.0.1:5173"
                    or "http://localhost:5174"
                    or "http://127.0.0.1:5174")
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
            .AllowAnyMethod();
    });
});

builder.Services.AddApplicationDatabase(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddApplicationAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

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

app.UseCors("StayFlowFrontendDevelopment");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<PermissionAuthorizationMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();