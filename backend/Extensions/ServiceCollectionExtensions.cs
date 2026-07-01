namespace StayFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<Repositories.ICompanyRepository, Repositories.CompanyRepository>();
        services.AddScoped<Services.ICompanyService, Services.CompanyService>();

        return services;
    }
}
