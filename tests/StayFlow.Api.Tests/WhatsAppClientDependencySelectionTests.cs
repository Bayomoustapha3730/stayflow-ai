using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StayFlow.Api.Extensions;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class WhatsAppClientDependencySelectionTests
{
    [Fact]
    public void AddApplicationServices_DevelopmentModeInDevelopment_ResolvesNonNetworkedClient()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsAppCloud:DevelopmentMode"] = "true",
                ["WhatsAppCloud:GraphApiBaseUrl"] = "https://graph.facebook.com",
                ["WhatsAppCloud:RequestTimeoutSeconds"] = "10"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Development"));
        services.AddLogging();
        services.AddApplicationServices(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IWhatsAppCloudClient>();

        Assert.IsType<DevelopmentWhatsAppCloudClient>(client);
        Assert.IsNotType<WhatsAppCloudClient>(client);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "StayFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}