namespace StayFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSignalR();
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
        services.AddOptions<Services.ConversationOptions>()
            .Bind(configuration.GetSection(Services.ConversationOptions.SectionName))
            .Validate(options => options.MaxMessageCharacters >= 1 && options.MaxMessageCharacters <= 4000, "Conversation message limit must be between 1 and 4000.")
            .Validate(options => options.ReuseOpenConversationMinutes >= 0 && options.ReuseOpenConversationMinutes <= 10080, "Conversation reuse window must be between 0 and 10080 minutes.")
            .Validate(options => options.MaxHistoryMessages >= 1 && options.MaxHistoryMessages <= 500, "Conversation history limit must be between 1 and 500.")
            .ValidateOnStart();
        services.AddOptions<Services.AI.Context.ConversationContextLimits>()
            .Bind(configuration.GetSection(Services.AI.Context.ConversationContextLimits.SectionName))
            .Validate(options => options.MaxVisibleMessages is >= 1 and <= 100, "Conversation context visible message limit must be between 1 and 100.")
            .Validate(options => options.MaxMessageCharacters is >= 100 and <= 4000, "Conversation context max message characters must be between 100 and 4000.")
            .Validate(options => options.MaxTotalPromptContextCharacters is >= 1000 and <= 50000, "Conversation context total prompt characters must be between 1000 and 50000.")
            .Validate(options => options.MaxKnowledgeItems is >= 0 and <= 50, "Conversation context knowledge item limit must be between 0 and 50.")
            .Validate(options => options.MaxKnowledgeItemCharacters is >= 100 and <= 10000, "Conversation context max knowledge item characters must be between 100 and 10000.")
            .Validate(options => options.ContextScanPageSize is >= 1 and <= 500, "Conversation context scan page size must be between 1 and 500.")
            .ValidateOnStart();
        services.AddOptions<Services.AIProviderOptions>()
            .Bind(configuration.GetSection(Services.AIProviderOptions.SectionName))
            .Validate(
                options => options.Provider.Equals("Development", StringComparison.OrdinalIgnoreCase)
                    || options.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase),
                "AI provider must be either Development or OpenAI.")
            .ValidateOnStart();
        services.AddOptions<Services.OpenAIOptions>()
            .Bind(configuration.GetSection(Services.OpenAIOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<Services.AI.Orchestration.AIReplyOrchestratorOptions>()
            .Bind(configuration.GetSection(Services.AI.Orchestration.AIReplyOrchestratorOptions.SectionName))
            .Validate(options => options.ProviderTimeoutSeconds is >= 3 and <= 60, "AI reply orchestrator timeout must be between 3 and 60 seconds.")
            .Validate(options => options.MaximumSelectedKnowledgeItems is >= 1 and <= 8, "AI reply orchestrator selected knowledge item limit must be between 1 and 8.")
            .Validate(options => options.MaximumSelectedKnowledgeCharacters is >= 1000 and <= 30000, "AI reply orchestrator selected knowledge character limit must be between 1000 and 30000.")
            .ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<Services.OpenAIOptions>, Services.OpenAIOptionsValidator>();
        services.AddScoped<Services.ICurrentTenantContext, Services.CurrentTenantContext>();
        services.AddScoped<Repositories.ICompanyRepository, Repositories.CompanyRepository>();
        services.AddScoped<Services.ICompanyService, Services.CompanyService>();
        services.AddScoped<Repositories.IPropertyRepository, Repositories.PropertyRepository>();
        services.AddScoped<Services.IPropertyService, Services.PropertyService>();
        services.AddScoped<Repositories.IPropertyKnowledgeRepository, Repositories.PropertyKnowledgeRepository>();
        services.AddScoped<Services.IPropertyKnowledgeService, Services.PropertyKnowledgeService>();
        services.AddScoped<Repositories.IGuestRepository, Repositories.GuestRepository>();
        services.AddScoped<Services.IGuestService, Services.GuestService>();
        services.AddScoped<Repositories.IConversationRepository, Repositories.ConversationRepository>();
        services.AddScoped<Services.IConversationService, Services.ConversationService>();
        services.AddScoped<Repositories.IReservationRepository, Repositories.ReservationRepository>();
        services.AddSingleton<Services.IReservationStatusTransitionPolicy, Services.ReservationStatusTransitionPolicy>();
        services.AddScoped<Services.IReservationService, Services.ReservationService>();
        services.AddScoped<Repositories.IConversationRepository, Repositories.ConversationRepository>();
        services.AddSingleton<Services.IConversationStatusTransitionPolicy, Services.ConversationStatusTransitionPolicy>();
        services.AddScoped<Services.IConversationService, Services.ConversationService>();
        services.AddScoped<Services.IConversationAIExchangeService, Services.ConversationAIExchangeService>();
        services.AddScoped<Services.AI.Context.IConversationContextBuilder, Services.AI.Context.ConversationContextBuilder>();
        services.AddSingleton<Services.AI.Context.IContextConfidenceEvaluator, Services.AI.Context.ContextConfidenceEvaluator>();
        services.AddSingleton<Services.AI.Intent.IGuestIntentDetector, Services.AI.Intent.GuestIntentDetector>();
        services.AddSingleton<Services.AI.Retrieval.IPropertyKnowledgeRanker, Services.AI.Retrieval.PropertyKnowledgeRanker>();
        services.AddSingleton<Services.AI.Validation.IAIReplyOutputValidator, Services.AI.Validation.AIReplyOutputValidator>();
        services.AddSingleton<Services.AI.Safety.IAIReplySafetyEvaluator, Services.AI.Safety.AIReplySafetyEvaluator>();
        services.AddSingleton<Services.AI.Orchestration.IAIReplyFallbackProvider, Services.AI.Orchestration.AIReplyFallbackProvider>();
        services.AddScoped<Services.AI.Orchestration.IAIReplyOrchestrator, Services.AI.Orchestration.AIReplyOrchestrator>();
        services.AddScoped<Services.ICopilotService, Services.CopilotService>();
        services.AddScoped<Services.IConversationRealtimePublisher, Services.ConversationRealtimePublisher>();
        services.AddScoped<Services.IChatService, Services.ChatService>();
        services.AddScoped<Services.IReservationContextResolver, Services.ReservationContextResolver>();
        services.AddScoped<Repositories.IAIContextRepository, Repositories.AIContextRepository>();
        services.AddSingleton<Services.IQuestionRelevanceClassifier, Services.KeywordQuestionRelevanceClassifier>();
        services.AddScoped<Services.IAIContextBuilder, Services.AIContextBuilder>();
        services.AddSingleton<Services.IAIPromptBuilder, Services.AIPromptBuilder>();
        services.AddScoped<Services.IAIResponseValidator, Services.AIResponseValidator>();
        services.AddScoped<Services.DevelopmentAIProvider>();
        services.AddScoped<Services.OpenAIAIProvider>();
        services.AddSingleton<Services.IOpenAIResponsesClient, Services.OpenAIResponsesClient>();
        services.AddScoped<Services.IAIProvider>(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Services.AIProviderOptions>>().Value.Provider;
            return provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<Services.OpenAIAIProvider>()
                : serviceProvider.GetRequiredService<Services.DevelopmentAIProvider>();
        });
        services.AddScoped<Services.IAIOrchestrator, Services.AIOrchestrator>();
        services.AddScoped<Repositories.IAuthRepository, Repositories.AuthRepository>();
        services.AddScoped<Services.IPasswordHasher, Services.Pbkdf2PasswordHasher>();
        services.AddScoped<Services.IJwtTokenService, Services.JwtTokenService>();
        services.AddScoped<Services.IAuthService, Services.AuthService>();
        services.AddScoped<Services.IRoleService, Services.RoleService>();
        services.AddScoped<Services.IDevelopmentSeedService, Services.DevelopmentSeedService>();

        return services;
    }
}
