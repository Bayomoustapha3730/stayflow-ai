using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;

namespace StayFlow.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddApplicationDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
