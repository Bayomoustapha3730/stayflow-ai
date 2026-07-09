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
        services.AddOptions<Services.AIContextOptions>()
            .Bind(configuration.GetSection(Services.AIContextOptions.SectionName))
            .Validate(options => options.MaxKnowledgeArticles >= 0 && options.MaxKnowledgeArticles <= 50, "AI context knowledge article limit must be between 0 and 50.")
            .Validate(options => options.MaxRecommendations >= 0 && options.MaxRecommendations <= 50, "AI context recommendation limit must be between 0 and 50.")
            .Validate(options => options.MaxHouseRules >= 0 && options.MaxHouseRules <= 50, "AI context house rule limit must be between 0 and 50.")
            .Validate(options => options.MaxEmergencyContacts >= 0 && options.MaxEmergencyContacts <= 50, "AI context emergency contact limit must be between 0 and 50.")
            .ValidateOnStart();
        services.AddOptions<Services.AIPromptOptions>()
            .Bind(configuration.GetSection(Services.AIPromptOptions.SectionName))
            .Validate(options => options.MaxResponseCharacters >= 200 && options.MaxResponseCharacters <= 4000, "AI prompt response character limit must be between 200 and 4000.")
            .ValidateOnStart();
        services.AddOptions<Services.AIProviderOptions>()
            .Bind(configuration.GetSection(Services.AIProviderOptions.SectionName))
            .Validate(options => options.Provider.Equals("Development", StringComparison.OrdinalIgnoreCase), "Only the Development AI provider is supported in this build.")
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
        services.AddScoped<Repositories.IAIContextRepository, Repositories.AIContextRepository>();
        services.AddSingleton<Services.IQuestionRelevanceClassifier, Services.KeywordQuestionRelevanceClassifier>();
        services.AddScoped<Services.IAIContextBuilder, Services.AIContextBuilder>();
        services.AddSingleton<Services.IAIPromptBuilder, Services.AIPromptBuilder>();
        services.AddScoped<Services.IAIResponseValidator, Services.AIResponseValidator>();
        services.AddScoped<Services.DevelopmentAIProvider>();
        services.AddScoped<Services.IAIProvider, Services.DevelopmentAIProvider>();
        services.AddScoped<Services.IAIOrchestrator, Services.AIOrchestrator>();
        services.AddScoped<Repositories.IAuthRepository, Repositories.AuthRepository>();
        services.AddScoped<Services.IPasswordHasher, Services.Pbkdf2PasswordHasher>();
        services.AddScoped<Services.IJwtTokenService, Services.JwtTokenService>();
        services.AddScoped<Services.IAuthService, Services.AuthService>();
        services.AddScoped<Services.IRoleService, Services.RoleService>();

        return services;
    }
}
