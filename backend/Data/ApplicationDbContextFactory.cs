using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using StayFlow.Api.Configuration;

namespace StayFlow.Api.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var hostConfiguration = new ConfigurationBuilder()
            .AddEnvironmentVariables("ASPNETCORE_")
            .AddEnvironmentVariables("DOTNET_")
            .AddCommandLine(args)
            .Build();
        var environment = hostConfiguration["environment"] ?? "Development";
        var basePath = ResolveBasePath();

        var configurationBuilder = new ConfigurationBuilder()
            .AddConfiguration(hostConfiguration)
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true);

        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddUserSecrets<ApplicationDbContextFactory>(optional: true);
        }

        var configuration = configurationBuilder
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        StripeTestEnvironmentGuard.ValidateAndCreate(configuration, environment);

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string ResolveBasePath()
    {
        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

        for (var directory = currentDirectory; directory is not null; directory = directory.Parent)
        {
            var appsettingsPath = Path.Combine(directory.FullName, "appsettings.json");
            if (File.Exists(appsettingsPath))
            {
                return directory.FullName;
            }

            var backendAppsettingsPath = Path.Combine(directory.FullName, "backend", "appsettings.json");
            if (File.Exists(backendAppsettingsPath))
            {
                return Path.Combine(directory.FullName, "backend");
            }
        }

        throw new InvalidOperationException("Unable to locate backend appsettings.json for design-time DbContext creation.");
    }
}
