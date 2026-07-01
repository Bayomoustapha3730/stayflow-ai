namespace StayFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<Repositories.ICompanyRepository, Repositories.CompanyRepository>();
        services.AddScoped<Services.ICompanyService, Services.CompanyService>();
        services.AddScoped<Repositories.IPropertyRepository, Repositories.PropertyRepository>();
        services.AddScoped<Services.IPropertyService, Services.PropertyService>();
        services.AddScoped<Repositories.IAuthRepository, Repositories.AuthRepository>();
        services.AddScoped<Services.IPasswordHasher, Services.Pbkdf2PasswordHasher>();
        services.AddScoped<Services.IJwtTokenService, Services.JwtTokenService>();
        services.AddScoped<Services.IAuthService, Services.AuthService>();
        services.AddScoped<Services.IRoleService, Services.RoleService>();

        return services;
    }
}
