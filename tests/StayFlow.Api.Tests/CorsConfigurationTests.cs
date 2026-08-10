using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StayFlow.Api.Configuration;

namespace StayFlow.Api.Tests;

public sealed class CorsConfigurationTests
{
    [Fact]
    public void ResolveAllowedOrigins_AddsLocalDevelopmentOrigins_WhenNoOriginsConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = ""
            })
            .Build();

        var environment = new FakeHostEnvironment("Development");

        var origins = CorsPolicyConfiguration.ResolveAllowedOrigins(configuration, environment);

        Assert.Contains("http://localhost:5173", origins);
        Assert.Contains("http://127.0.0.1:5173", origins);
        Assert.Contains("https://*.app.github.dev", CorsPolicyConfiguration.ResolveAllowedOriginPatterns(configuration, environment));
    }

    [Fact]
    public void ResolveAllowedOrigins_PreservesExplicitOrigins_AndDoesNotDuplicateDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://example.com",
                ["Cors:AllowedOriginPatterns:0"] = "https://*.example.dev"
            })
            .Build();

        var environment = new FakeHostEnvironment("Development");

        var origins = CorsPolicyConfiguration.ResolveAllowedOrigins(configuration, environment);
        var patterns = CorsPolicyConfiguration.ResolveAllowedOriginPatterns(configuration, environment);

        Assert.Contains("https://example.com", origins);
        Assert.Contains("http://localhost:5173", origins);
        Assert.Equal(2, patterns.Length);
        Assert.Contains("https://*.example.dev", patterns);
        Assert.Contains("https://*.app.github.dev", patterns);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "StayFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string? ContentRootPathOverride { get; set; }
    }
}
