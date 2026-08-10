using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace StayFlow.Api.Configuration;

public static partial class CorsPolicyConfiguration
{
    public const string PolicyName = "StayFlowFrontendDevelopment";

    private const string DevelopmentCodespacesPattern = "https://*.app.github.dev";

    public static string[] ResolveAllowedOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var normalizedOrigins = configuredOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (environment.IsDevelopment())
        {
            foreach (var fallbackOrigin in new[] { "http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://127.0.0.1:5174" })
            {
                if (!normalizedOrigins.Contains(fallbackOrigin, StringComparer.OrdinalIgnoreCase))
                {
                    normalizedOrigins.Add(fallbackOrigin);
                }
            }
        }

        return normalizedOrigins.ToArray();
    }

    public static string[] ResolveAllowedOriginPatterns(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredPatterns = configuration.GetSection("Cors:AllowedOriginPatterns").Get<string[]>() ?? [];
        var normalizedPatterns = configuredPatterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (environment.IsDevelopment() && !normalizedPatterns.Contains(DevelopmentCodespacesPattern, StringComparer.OrdinalIgnoreCase))
        {
            normalizedPatterns.Insert(0, DevelopmentCodespacesPattern);
        }

        return normalizedPatterns.ToArray();
    }

    public static void ConfigurePolicy(CorsPolicyBuilder policy, IConfiguration configuration, IHostEnvironment environment)
    {
        var allowedOrigins = ResolveAllowedOrigins(configuration, environment);
        var allowedOriginPatterns = ResolveAllowedOriginPatterns(configuration, environment);

        if (allowedOrigins.Any(origin => origin == "*"))
        {
            throw new InvalidOperationException("Cors:AllowedOrigins must not contain wildcard '*' when credentials are enabled.");
        }

        if (allowedOriginPatterns.Any(pattern => pattern == "*"))
        {
            throw new InvalidOperationException("Cors:AllowedOriginPatterns must not contain wildcard '*' when credentials are enabled.");
        }

        policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .AllowAnyHeader()
            .AllowCredentials();

        if (allowedOriginPatterns.Length > 0)
        {
            policy.SetIsOriginAllowed(origin => IsOriginAllowed(origin, allowedOrigins, allowedOriginPatterns));
        }
    }

    public static bool IsOriginAllowed(string origin, string[] allowedOrigins, string[] allowedOriginPatterns)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return allowedOriginPatterns.Any(pattern => MatchesPattern(origin, pattern));
    }

    private static bool MatchesPattern(string origin, string pattern)
    {
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(origin, regexPattern, RegexOptions.IgnoreCase);
        }

        return string.Equals(origin, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
