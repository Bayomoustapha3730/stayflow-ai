using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using StayFlow.Api.Authorization;
using StayFlow.Api.Middleware;
using StayFlow.Api.Models;

namespace StayFlow.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApplicationAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = "BearerOrApiKey";
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme("BearerOrApiKey", "BearerOrApiKey", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authorization = context.Request.Headers.Authorization.ToString();
                    return authorization.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase)
                        ? ApiKeyAuthenticationDefaults.Scheme
                        : JwtBearerDefaults.AuthenticationScheme;
                };
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        var correlationId = context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
                            ? value?.ToString()
                            : null;
                        var traceId = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

                        var problem = new ProblemDetails
                        {
                            Type = "https://httpstatuses.com/401",
                            Title = "Unauthorized",
                            Status = StatusCodes.Status401Unauthorized,
                            Detail = "Authentication is required to access this resource.",
                            Instance = context.Request.Path
                        };

                        problem.Extensions["traceId"] = traceId;
                        if (!string.IsNullOrWhiteSpace(correlationId))
                        {
                            problem.Extensions["correlationId"] = correlationId;
                        }

                        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/problem+json";

                        var correlationId = context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
                            ? value?.ToString()
                            : null;
                        var traceId = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;

                        var problem = new ProblemDetails
                        {
                            Type = "https://httpstatuses.com/403",
                            Title = "Forbidden",
                            Status = StatusCodes.Status403Forbidden,
                            Detail = "You are not allowed to perform this operation.",
                            Instance = context.Request.Path
                        };

                        problem.Extensions["traceId"] = traceId;
                        if (!string.IsNullOrWhiteSpace(correlationId))
                        {
                            problem.Extensions["correlationId"] = correlationId;
                        }

                        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
                    },
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrWhiteSpace(accessToken)
                            && path.StartsWithSegments("/hubs/conversations", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            })
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationDefaults.Scheme,
                _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(OrganizationPolicyNames.ReadOnly, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new OrganizationRoleRequirement(OrganizationRole.ReadOnly)));
            options.AddPolicy(OrganizationPolicyNames.Support, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new OrganizationRoleRequirement(OrganizationRole.Support)));
            options.AddPolicy(OrganizationPolicyNames.Host, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new OrganizationRoleRequirement(OrganizationRole.Host)));
            options.AddPolicy(OrganizationPolicyNames.Manager, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new OrganizationRoleRequirement(OrganizationRole.Manager)));
            options.AddPolicy(OrganizationPolicyNames.Administrator, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new OrganizationRoleRequirement(OrganizationRole.Administrator)));
            options.AddPolicy(OrganizationPolicyNames.Owner, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new OrganizationRoleRequirement(OrganizationRole.Owner)));
            options.AddPolicy(OrganizationPolicyNames.PlatformAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    context.User.HasClaim("permission", "platform.admin")
                    || context.User.HasClaim("platform_admin", "true")
                    || context.User.IsInRole("PlatformAdmin")));

            options.AddPolicy(ApiKeyPolicyNames.IntegrationsRead, policy =>
                policy.AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim("api_scope", ApiKeyScope.IntegrationsRead));

            options.AddPolicy(ApiKeyPolicyNames.UsageRead, policy =>
                policy.AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim("api_scope", ApiKeyScope.UsageRead));

            options.AddPolicy(ApiKeyPolicyNames.ConversationsRead, policy =>
                policy.AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.Scheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim("api_scope", ApiKeyScope.ConversationsRead));
        });
        services.AddScoped<IAuthorizationHandler, OrganizationRoleAuthorizationHandler>();

        return services;
    }
}
