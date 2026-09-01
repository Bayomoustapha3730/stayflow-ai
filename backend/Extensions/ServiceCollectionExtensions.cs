using Microsoft.Extensions.DependencyInjection.Extensions;

namespace StayFlow.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 64 * 1024;
        });
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient();
        services.AddTransient<Services.OutboundCorrelationHandler>();
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
        services.AddOptions<Services.AI.Retrieval.KnowledgeRetrievalOptions>()
            .Bind(configuration.GetSection(Services.AI.Retrieval.KnowledgeRetrievalOptions.SectionName))
            .Validate(options => options.MaxCandidates is >= 1 and <= 100, "Knowledge retrieval max candidates must be between 1 and 100.")
            .Validate(options => options.TopCandidateCount is >= 1 and <= 10, "Knowledge retrieval top candidate count must be between 1 and 10.")
            .Validate(options => options.MaxSelectedItems is >= 1 and <= 8, "Knowledge retrieval max selected items must be between 1 and 8.")
            .Validate(options => options.MinimumScore is >= -100 and <= 200, "Knowledge retrieval minimum score must be between -100 and 200.")
            .Validate(options => options.MinimumConfidenceScore is >= 0 and <= 100, "Knowledge retrieval minimum confidence score must be between 0 and 100.")
            .Validate(options => options.HighConfidenceScore is >= 0 and <= 100, "Knowledge retrieval high confidence score must be between 0 and 100.")
            .Validate(options => options.MediumConfidenceScore is >= 0 and <= 100, "Knowledge retrieval medium confidence score must be between 0 and 100.")
            .Validate(options => options.MinimumScoreGap is >= 0 and <= 50, "Knowledge retrieval minimum score gap must be between 0 and 50.")
            .Validate(options => options.ContextCharacterBudget is >= 1000 and <= 30000, "Knowledge retrieval context character budget must be between 1000 and 30000.")
            .Validate(options => options.EmergencyMismatchPenalty is >= 0 and <= 500, "Knowledge retrieval emergency mismatch penalty must be between 0 and 500.")
            .ValidateOnStart();
        services.AddOptions<Services.AI.Retrieval.KnowledgeRerankerOptions>()
            .Bind(configuration.GetSection(Services.AI.Retrieval.KnowledgeRerankerOptions.SectionName))
            .Validate(options => options.PriorSelectionBoost is >= 0 and <= 0.3, "Knowledge reranker prior selection boost must be between 0 and 0.3.")
            .Validate(options => options.ClarificationTopicBoost is >= 0 and <= 0.3, "Knowledge reranker clarification topic boost must be between 0 and 0.3.")
            .ValidateOnStart();
        services.AddOptions<Services.AI.Retrieval.KnowledgeEmbeddingOptions>()
            .Bind(configuration.GetSection(Services.AI.Retrieval.KnowledgeEmbeddingOptions.SectionName))
            .Validate(options => options.EmbeddingWeight is >= 0 and <= 0.5, "Knowledge embedding blend weight must be between 0 and 0.5.")
            .ValidateOnStart();
        services.AddOptions<Services.AI.Orchestration.ConciergeIntelligenceOptions>()
            .Bind(configuration.GetSection(Services.AI.Orchestration.ConciergeIntelligenceOptions.SectionName))
            .Validate(options => options.RecentMessageCount is >= 4 and <= 20, "Concierge intelligence recent message count must be between 4 and 20.")
            .Validate(options => options.MemoryCharacterBudget is >= 300 and <= 20000, "Concierge intelligence memory character budget must be between 300 and 20000.")
            .Validate(options => options.MaximumIntents is >= 1 and <= 3, "Concierge intelligence maximum intents must be between 1 and 3.")
            .Validate(options => options.MaximumCandidates is >= 1 and <= 30, "Concierge intelligence maximum candidates must be between 1 and 30.")
            .Validate(options => options.MaximumSelectedItems is >= 1 and <= 5, "Concierge intelligence maximum selected items must be between 1 and 5.")
            .Validate(options => options.ContextCharacterBudget is >= 1000 and <= 30000, "Concierge intelligence context character budget must be between 1000 and 30000.")
            .Validate(options => options.HighConfidenceThreshold > options.MediumConfidenceThreshold, "Concierge intelligence high confidence threshold must be greater than medium confidence threshold.")
            .Validate(options => options.IntentWeight >= 0 && options.LexicalWeight >= 0 && options.SemanticWeight >= 0 && options.PriorityWeight >= 0,
                "Concierge intelligence component weights must be nonnegative.")
            .Validate(options => Math.Abs((options.IntentWeight + options.LexicalWeight + options.SemanticWeight + options.PriorityWeight) - 1.0) <= 0.25,
                "Concierge intelligence component weights should be approximately normalized.")
            .ValidateOnStart();
        services.AddOptions<Services.AI.Orchestration.GroundedConciergeOptions>()
            .Bind(configuration.GetSection(Services.AI.Orchestration.GroundedConciergeOptions.SectionName))
            .Validate(options => options.ProviderTimeoutSeconds is >= 3 and <= 60, "Grounded concierge provider timeout must be between 3 and 60 seconds.")
            .Validate(options => options.MaximumResponseCharacters is >= 200 and <= 4000, "Grounded concierge maximum response characters must be between 200 and 4000.")
            .Validate(options => options.MaximumKnowledgeCharacters is >= 1000 and <= 30000, "Grounded concierge maximum knowledge characters must be between 1000 and 30000.")
            .ValidateOnStart();
        services.AddOptions<Services.AI.Orchestration.DevelopmentConciergeLanguageModelOptions>()
            .Bind(configuration.GetSection(Services.AI.Orchestration.DevelopmentConciergeLanguageModelOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<Services.ConciergeActions.ConciergeActionsOptions>()
            .Bind(configuration.GetSection(Services.ConciergeActions.ConciergeActionsOptions.SectionName))
            .Validate(options => options.PendingActionExpirationMinutes is >= 5 and <= 1440, "Concierge action pending expiration must be between 5 and 1440 minutes.")
            .Validate(options => options.MaximumActionsPerConversationPerHour is >= 1 and <= 100, "Concierge action maximum per conversation must be between 1 and 100 per hour.")
            .Validate(options => options.MaximumExtraItemQuantity is >= 1 and <= 20, "Concierge action maximum extra item quantity must be between 1 and 20.")
            .Validate(options => options.MaximumVehicleCount is >= 1 and <= 10, "Concierge action maximum vehicle count must be between 1 and 10.")
            .Validate(options => options.MaximumNoteLength is >= 20 and <= 500, "Concierge action note length must be between 20 and 500 characters.")
            .ValidateOnStart();
        services.AddOptions<Services.HostCopilot.HostCopilotOptions>()
            .Bind(configuration.GetSection(Services.HostCopilot.HostCopilotOptions.SectionName))
            .Validate(options => options.NormalPrioritySlaMinutes is >= 1 and <= 240, "Host copilot normal SLA must be between 1 and 240 minutes.")
            .Validate(options => options.HighPrioritySlaMinutes is >= 1 and <= 240, "Host copilot high SLA must be between 1 and 240 minutes.")
            .Validate(options => options.UrgentPrioritySlaMinutes is >= 1 and <= 240, "Host copilot urgent SLA must be between 1 and 240 minutes.")
            .Validate(options => options.MaximumTimelineEvents is >= 3 and <= 50, "Host copilot timeline event limit must be between 3 and 50.")
            .ValidateOnStart();
        services.AddOptions<Services.WhatsAppCloudOptions>()
            .Bind(configuration.GetSection(Services.WhatsAppCloudOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<Services.Payments.MpesaOptions>()
            .Bind(configuration.GetSection(Services.Payments.MpesaOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<Services.Payments.MpesaOptions>, Services.Payments.MpesaOptionsValidator>();
        services.AddOptions<Services.Billing.BillingOptions>()
            .Bind(configuration.GetSection(Services.Billing.BillingOptions.SectionName))
            .Validate(options => options.Provider.Equals("Development", StringComparison.OrdinalIgnoreCase)
                || options.Provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase),
                "Billing provider must be either Development or Stripe.")
            .Validate(options => options.WebhookToleranceSeconds is >= 30 and <= 3600,
                "Billing webhook tolerance must be between 30 and 3600 seconds.")
            .Validate(options => options.WebhookMaxBodyBytes is >= 16 * 1024 and <= 1024 * 1024,
                "Billing webhook max body size must be between 16KB and 1MB.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CheckoutSuccessUrl)
                && !string.IsNullOrWhiteSpace(options.CheckoutCancelUrl)
                && !string.IsNullOrWhiteSpace(options.BillingPortalReturnUrl),
                "Billing URLs must be configured.")
            .ValidateOnStart();
        services.AddOptions<StayFlow.Api.Configuration.EmailDeliveryOptions>()
            .Bind(configuration.GetSection(StayFlow.Api.Configuration.EmailDeliveryOptions.SectionName))
            .Validate(options => options.Provider.Equals("Development", StringComparison.OrdinalIgnoreCase)
                || options.Provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase)
                || options.Provider.Equals("SendGrid", StringComparison.OrdinalIgnoreCase)
                || options.Provider.Equals("AzureCommunicationServices", StringComparison.OrdinalIgnoreCase),
                "Email provider must be Development, Smtp, SendGrid, or AzureCommunicationServices.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress), "Email from address must be configured.")
            .ValidateOnStart();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<Services.WhatsAppCloudOptions>, Services.WhatsAppCloudOptionsValidator>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<Services.OpenAIOptions>, Services.OpenAIOptionsValidator>();
        services.AddSingleton<Services.Email.DevelopmentEmailInbox>();
        services.AddSingleton<Services.Email.DevelopmentEmailSender>();
        services.AddScoped<Services.Email.SmtpEmailSender>();
        services.AddScoped<Services.Email.SendGridCompatibleEmailSender>();
        services.AddScoped<Services.Email.AzureCommunicationServicesCompatibleEmailSender>();
        services.AddScoped<Services.Email.IEmailSender>(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<StayFlow.Api.Configuration.EmailDeliveryOptions>>().Value.Provider;
            return provider.ToUpperInvariant() switch
            {
                "SMTP" => serviceProvider.GetRequiredService<Services.Email.SmtpEmailSender>(),
                "SENDGRID" => serviceProvider.GetRequiredService<Services.Email.SendGridCompatibleEmailSender>(),
                "AZURECOMMUNICATIONSERVICES" => serviceProvider.GetRequiredService<Services.Email.AzureCommunicationServicesCompatibleEmailSender>(),
                _ => serviceProvider.GetRequiredService<Services.Email.DevelopmentEmailSender>()
            };
        });
        services.AddScoped<Services.Email.IIdentityEmailService, Services.Email.IdentityEmailService>();
        services.AddScoped<Services.ITenantExecutionContextAccessor, Services.TenantExecutionContextAccessor>();
        services.AddScoped<Services.TenantContext>();
        services.AddScoped<Services.ITenantContext>(serviceProvider => serviceProvider.GetRequiredService<Services.TenantContext>());
        services.AddScoped<Services.ICurrentTenantContext>(serviceProvider => serviceProvider.GetRequiredService<Services.TenantContext>());
        services.AddScoped<Repositories.ICompanyRepository, Repositories.CompanyRepository>();
        services.AddScoped<Services.ICompanyService, Services.CompanyService>();
        services.AddScoped<Services.IOrganizationService, Services.OrganizationService>();
        services.AddScoped<Services.ISubscriptionEntitlementService, Services.SubscriptionEntitlementService>();
        services.AddScoped<Services.IOnboardingService, Services.OnboardingService>();
        services.AddScoped<Services.IOrganizationInvitationService, Services.OrganizationInvitationService>();
        services.AddScoped<Services.ITenantApiKeyService, Services.TenantApiKeyService>();
        services.AddScoped<Services.IBillingService, Services.BillingService>();
        services.AddScoped<Services.Billing.DevelopmentBillingProvider>();
        services.AddScoped<Services.Billing.StripeBillingProvider>();
        services.AddScoped<Services.Billing.IBillingProvider>(serviceProvider =>
        {
            var provider = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Services.Billing.BillingOptions>>().Value.Provider;
            return provider.Equals("Stripe", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<Services.Billing.StripeBillingProvider>()
                : serviceProvider.GetRequiredService<Services.Billing.DevelopmentBillingProvider>();
        });
        services.AddScoped<Repositories.IPropertyRepository, Repositories.PropertyRepository>();
        services.AddScoped<Services.IPropertyService, Services.PropertyService>();
        services.AddScoped<Repositories.IPropertyKnowledgeRepository, Repositories.PropertyKnowledgeRepository>();
        services.AddScoped<Services.IPropertyKnowledgeService, Services.PropertyKnowledgeService>();
        services.AddScoped<Repositories.IGuestRepository, Repositories.GuestRepository>();
        services.AddScoped<Services.IGuestService, Services.GuestService>();
        services.AddScoped<Repositories.IConversationRepository, Repositories.ConversationRepository>();
        services.AddScoped<Repositories.IWhatsAppRepository, Repositories.WhatsAppRepository>();
        services.AddScoped<Services.IConversationService, Services.ConversationService>();
        services.AddScoped<Repositories.IReservationRepository, Repositories.ReservationRepository>();
        services.AddSingleton<Services.IReservationStatusTransitionPolicy, Services.ReservationStatusTransitionPolicy>();
        services.AddScoped<Services.IReservationLifecycleService, Services.ReservationLifecycleService>();
        services.AddScoped<Services.IReservationService, Services.ReservationService>();
        services.AddScoped<Repositories.IConversationRepository, Repositories.ConversationRepository>();
        services.AddSingleton<Services.IConversationStatusTransitionPolicy, Services.ConversationStatusTransitionPolicy>();
        services.AddScoped<Services.IConversationService, Services.ConversationService>();
        services.AddScoped<Services.IConversationAIExchangeService, Services.ConversationAIExchangeService>();
        services.AddScoped<Services.AI.Context.IConversationContextBuilder, Services.AI.Context.ConversationContextBuilder>();
        services.AddSingleton<Services.AI.Context.IContextConfidenceEvaluator, Services.AI.Context.ContextConfidenceEvaluator>();
        services.AddSingleton<Services.AI.Intent.IConversationIntentRecognizer, Services.AI.Intent.ConversationIntentRecognizer>();
        services.AddSingleton<Services.AI.Intent.IGuestIntentDetector, Services.AI.Intent.GuestIntentDetector>();
        services.AddSingleton<Services.AI.Memory.IConversationSummaryService, Services.AI.Memory.DeterministicConversationSummaryService>();
        services.AddSingleton<Services.AI.Memory.IConversationMemoryService, Services.AI.Memory.ConversationMemoryService>();
        services.AddSingleton<Services.AI.Retrieval.IKnowledgeEmbeddingProvider, Services.AI.Retrieval.NoOpKnowledgeEmbeddingProvider>();
        services.AddSingleton<Services.AI.Retrieval.IKnowledgeReranker, Services.AI.Retrieval.DeterministicKnowledgeReranker>();
        services.AddSingleton<Services.AI.Retrieval.IKnowledgeQueryExpander, Services.AI.Retrieval.KnowledgeQueryExpander>();
        services.AddSingleton<Services.AI.Retrieval.IKnowledgeSemanticSimilarityService, Services.AI.Retrieval.DeterministicKnowledgeSemanticSimilarityService>();
        services.AddSingleton<Services.AI.Retrieval.IKnowledgeSimilarityScorer, Services.AI.Retrieval.DeterministicKnowledgeSimilarityScorer>();
        services.AddSingleton<Services.AI.Retrieval.IPropertyKnowledgeRanker, Services.AI.Retrieval.PropertyKnowledgeRanker>();
        services.AddSingleton<Services.AI.Retrieval.IPropertyKnowledgeRetriever, Services.AI.Retrieval.PropertyKnowledgeRetriever>();
        services.AddSingleton<Services.AI.Orchestration.IConciergeResponseGenerator, Services.AI.Orchestration.ConciergeResponseGenerator>();
        services.AddSingleton<Services.AI.Orchestration.IConciergePromptBuilder, Services.AI.Orchestration.ConciergePromptBuilder>();
        services.AddSingleton<Services.AI.Orchestration.IConciergeResponseValidator, Services.AI.Orchestration.ConciergeResponseValidator>();
        services.AddSingleton<Services.AI.Orchestration.IConciergeLanguageModel, Services.AI.Orchestration.DevelopmentConciergeLanguageModel>();
        services.AddSingleton<Services.AI.Orchestration.IConciergeLanguageModelProviderFactory, Services.AI.Orchestration.ConciergeLanguageModelProviderFactory>();
        services.AddSingleton<Services.AI.Orchestration.IGroundedConciergeResponseGenerator, Services.AI.Orchestration.GroundedConciergeResponseGenerator>();
        services.AddSingleton<Services.AI.Validation.IAIReplyOutputValidator, Services.AI.Validation.AIReplyOutputValidator>();
        services.AddSingleton<Services.AI.Safety.IAIReplySafetyEvaluator, Services.AI.Safety.AIReplySafetyEvaluator>();
        services.AddSingleton<Services.AI.Orchestration.IAIReplyFallbackProvider, Services.AI.Orchestration.AIReplyFallbackProvider>();
        services.AddScoped<Services.AI.Orchestration.IAIReplyOrchestrator, Services.AI.Orchestration.AIReplyOrchestrator>();
        services.AddScoped<Services.ICopilotService, Services.CopilotService>();
        services.AddScoped<Services.IConversationRealtimePublisher, Services.ConversationRealtimePublisher>();
        services.AddSingleton<Services.IWhatsAppDevelopmentMessageStore, Services.WhatsAppDevelopmentMessageStore>();
        services.AddScoped<Services.IPhoneNumberNormalizer, Services.PhoneNumberNormalizer>();
        services.AddHttpClient(nameof(Services.WhatsAppCloudClient), (serviceProvider, client) =>
        {
            var cloudOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Services.WhatsAppCloudOptions>>().Value;
            client.BaseAddress = new Uri(cloudOptions.GraphApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(cloudOptions.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("StayFlow-WhatsAppCloud/1.0");
        })
        .AddHttpMessageHandler<Services.OutboundCorrelationHandler>()
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });
        services.AddScoped<Services.WhatsAppCloudClient>();
        services.AddScoped<Services.DevelopmentWhatsAppCloudClient>();
        services.AddSingleton<Services.IWhatsAppProviderTelemetry, Services.WhatsAppProviderTelemetry>();
        services.AddScoped<Services.IWhatsAppCloudClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Services.WhatsAppCloudOptions>>().Value;
            var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
            if (options.DevelopmentMode)
            {
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException("WhatsAppCloud:DevelopmentMode cannot be enabled outside Development environment.");
                }

                return serviceProvider.GetRequiredService<Services.DevelopmentWhatsAppCloudClient>();
            }

            return serviceProvider.GetRequiredService<Services.WhatsAppCloudClient>();
        });
        services.AddScoped<Services.IConversationChannelSender, Services.WebConversationChannelSender>();
        services.AddScoped<Services.IConversationChannelSender, Services.WhatsAppConversationChannelSender>();
        services.AddScoped<Services.IConversationChannelDispatcher, Services.ConversationChannelDispatcher>();
        services.AddSingleton<Services.IWhatsAppWebhookQueue, Services.WhatsAppWebhookQueue>();
        services.AddScoped<Services.IWhatsAppWebhookSignatureVerifier, Services.WhatsAppWebhookSignatureVerifier>();
        services.AddHostedService<Services.WhatsAppWebhookBackgroundService>();
        services.AddScoped<Services.IWhatsAppWebhookProcessor, Services.WhatsAppWebhookProcessor>();
        services.AddScoped<Services.IWhatsAppCredentialResolver, Services.WhatsAppCredentialResolver>();
        services.AddScoped<Services.IWhatsAppIntegrationHealthService, Services.WhatsAppIntegrationHealthService>();
        services.AddScoped<Services.IWhatsAppTemplateService, Services.WhatsAppTemplateService>();
        services.AddScoped<Services.IWhatsAppCustomerServiceWindowEvaluator, Services.WhatsAppCustomerServiceWindowEvaluator>();
        services.AddSingleton<Services.IWhatsAppTemplateVariableValidator, Services.WhatsAppTemplateVariableValidator>();
        services.AddScoped<Services.IChatService, Services.ChatService>();
        services.AddScoped<Services.ConciergeActions.IConciergeActionDetector, Services.ConciergeActions.ConciergeActionDetector>();
        services.AddScoped<Services.ConciergeActions.IConciergeActionPolicy>(serviceProvider =>
        {
            var actionOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Services.ConciergeActions.ConciergeActionsOptions>>().Value;
            return new Services.ConciergeActions.ConciergeActionPolicy(actionOptions);
        });
        services.AddScoped<Services.ConciergeActions.IConciergeActionIdempotencyService, Services.ConciergeActions.ConciergeActionIdempotencyService>();
        services.AddScoped<Services.ConciergeActions.IConciergeActionAuditService, Services.ConciergeActions.ConciergeActionAuditService>();
        services.AddScoped<Services.ConciergeActions.IConciergeActionConfirmationService, Services.ConciergeActions.ConciergeActionConfirmationService>();
        services.AddScoped<Services.ConciergeActions.IConciergeActionResultFormatter, Services.ConciergeActions.ConciergeActionResultFormatter>();
        services.AddScoped<Services.ConciergeActions.EarlyCheckInRequestHandler>();
        services.AddScoped<Services.ConciergeActions.LateCheckoutRequestHandler>();
        services.AddScoped<Services.ConciergeActions.MaintenanceTicketHandler>();
        services.AddScoped<Services.ConciergeActions.HousekeepingRequestHandler>();
        services.AddScoped<Services.ConciergeActions.ExtraItemRequestHandler>();
        services.AddScoped<Services.ConciergeActions.ParkingRequestHandler>();
        services.AddScoped<Services.ConciergeActions.PaymentRequestHandler>();
        services.AddScoped<Services.ConciergeActions.HostNotificationHandler>();
        services.AddScoped<Services.ConciergeActions.IConciergeActionExecutor, Services.ConciergeActions.ConciergeActionExecutor>();
        services.AddScoped<Services.ConciergeActions.IConciergeActionOrchestrator, Services.ConciergeActions.ConciergeActionOrchestrator>();
        services.AddScoped<Services.ConciergeActions.IConciergeHostActionService, Services.ConciergeActions.ConciergeHostActionService>();
        services.AddScoped<Services.HostCopilot.IHostCopilotWorkspaceService, Services.HostCopilot.HostCopilotWorkspaceService>();
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
        services.AddScoped<Repositories.IPaymentRepository, Repositories.PaymentRepository>();
        services.AddScoped<Services.Payments.IReservationPaymentGroundingService, Services.Payments.ReservationPaymentGroundingService>();
        services.AddScoped<Services.Payments.IPostPaymentNotificationService, Services.Payments.PostPaymentNotificationService>();
        services.AddScoped<Services.Payments.IPaymentService, Services.Payments.PaymentService>();
        services.AddScoped<Services.Payments.IMpesaCredentialResolver, Services.Payments.MpesaCredentialResolver>();
        services.AddScoped<Services.Payments.IKenyanPhoneNumberNormalizer, Services.Payments.KenyanPhoneNumberNormalizer>();
        services.AddHttpClient(nameof(Services.Payments.MpesaApiClient), (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Services.Payments.MpesaOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });
        services.AddScoped<Services.Payments.IMpesaApiClient, Services.Payments.MpesaApiClient>();

        services.AddScoped<
            Services.Payments.IMpesaPaymentReconciliationService,
            Services.Payments.MpesaPaymentReconciliationService>();

        services.AddHostedService<
            Services.Payments.MpesaPaymentReconciliationWorker>();

        services.AddHttpClient(nameof(Services.Payments.MpesaHealthService));
        services.AddScoped<Services.Payments.IMpesaHealthService, Services.Payments.MpesaHealthService>();

        return services;
    }
}
