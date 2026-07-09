namespace StayFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddOptions<Services.ReservationContextOptions>()
            .Bind(configuration.GetSection(Services.ReservationContextOptions.SectionName))
            .Validate(options => options.PreArrivalWindowDays >= 0 && options.PreArrivalWindowDays <= 365, "Reservation context pre-arrival window must be between 0 and 365 days.")
            .ValidateOnStart();
        services.AddScoped<Services.ICurrentTenantContext, Services.CurrentTenantContext>();
        services.AddScoped<Repositories.ICompanyRepository, Repositories.CompanyRepository>();
        services.AddScoped<Services.ICompanyService, Services.CompanyService>();
        services.AddScoped<Repositories.IPropertyRepository, Repositories.PropertyRepository>();
        services.AddScoped<Services.IPropertyService, Services.PropertyService>();
        services.AddScoped<Repositories.IGuestRepository, Repositories.GuestRepository>();
        services.AddScoped<Services.IGuestService, Services.GuestService>();
        services.AddScoped<Repositories.IReservationRepository, Repositories.ReservationRepository>();
        services.AddSingleton<Services.IReservationStatusTransitionPolicy, Services.ReservationStatusTransitionPolicy>();
        services.AddScoped<Services.IReservationService, Services.ReservationService>();
        services.AddScoped<Services.IReservationContextResolver, Services.ReservationContextResolver>();
        services.AddScoped<Repositories.IAuthRepository, Repositories.AuthRepository>();
        services.AddScoped<Services.IPasswordHasher, Services.Pbkdf2PasswordHasher>();
        services.AddScoped<Services.IJwtTokenService, Services.JwtTokenService>();
        services.AddScoped<Services.IAuthService, Services.AuthService>();
        services.AddScoped<Services.IRoleService, Services.RoleService>();

        return services;
    }
}
