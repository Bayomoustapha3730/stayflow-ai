using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class WhatsAppTemplateService(
    IWhatsAppRepository whatsAppRepository,
    IConversationRepository conversationRepository,
    ICurrentTenantContext currentTenantContext,
    IConversationStatusTransitionPolicy transitionPolicy,
    IConversationRealtimePublisher realtimePublisher,
    IWhatsAppCloudClient whatsAppCloudClient,
    IWhatsAppCredentialResolver credentialResolver,
    IWhatsAppIntegrationHealthService healthService,
    IWhatsAppTemplateVariableValidator variableValidator,
    ISubscriptionEntitlementService subscriptionEntitlementService,
    IWhatsAppCustomerServiceWindowEvaluator windowEvaluator,
    IPhoneNumberNormalizer phoneNumberNormalizer,
    IHostEnvironment hostEnvironment,
    IOptions<WhatsAppCloudOptions> cloudOptions,
    ILogger<WhatsAppTemplateService> logger) : IWhatsAppTemplateService
{
    // Fixed by the AddWhatsAppMessagingFoundation migration; a violation here means a caller tried
    // to configure a PhoneNumberId already claimed by another integration.
    private const string PhoneNumberIdUniqueIndexName = "IX_WhatsAppIntegrations_PhoneNumberId";
    private static readonly Regex GraphApiVersionPattern = new(@"^v[0-9]{1,3}(\.[0-9]{1,2})?$", RegexOptions.Compiled);
    private static readonly Regex CredentialReferencePattern = new(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

    public async Task<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>> GetIntegrationsAsync(CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var error))
        {
            return ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>.Fail(error, [error]);
        }

        var integrations = await whatsAppRepository.ListIntegrationsForCompanyAsync(companyId, cancellationToken);
        var items = integrations.Select(integration => new WhatsAppIntegrationSummaryResponse
        {
            Id = integration.Id,
            DisplayName = integration.DisplayName,
            BusinessPhoneNumberMasked = integration.BusinessPhoneNumberMasked,
            IsActive = integration.IsActive,
            IsProductionEnabled = integration.IsProductionEnabled,
            Mode = integration.IsProductionEnabled ? "Production" : "Development",
            HealthStatus = string.IsNullOrWhiteSpace(integration.WebhookConfigurationStatus) ? "Unknown" : integration.WebhookConfigurationStatus,
            LastHealthCheckAt = integration.LastHealthCheckAt,
            LastSuccessfulHealthCheckAt = integration.LastSuccessfulHealthCheckAt,
            LastTemplateSyncAt = integration.LastTemplateSyncAt,
            LastErrorSummary = integration.LastErrorSummary
        }).ToList();

        return ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>.Ok(items);
    }

    public async Task<ApiResponse<WhatsAppIntegrationDetailResponse>> GetIntegrationDetailAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return ApiResponse<WhatsAppIntegrationDetailResponse>.Fail(integrationResult.ErrorMessage);
        }

        return ApiResponse<WhatsAppIntegrationDetailResponse>.Ok(MapDetail(integrationResult.Integration));
    }

    public async Task<ApiResponse<WhatsAppIntegrationDetailResponse>> CreateIntegrationAsync(WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<WhatsAppIntegrationDetailResponse>.Fail(tenantError, [tenantError]);
        }

        if (!TryValidateConfiguration(request, out var values, out var validationErrors))
        {
            return ApiResponse<WhatsAppIntegrationDetailResponse>.Fail("WhatsApp integration configuration is invalid.", validationErrors);
        }

        var integration = new WhatsAppIntegration
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DisplayName = values.DisplayName,
            PhoneNumberId = values.PhoneNumberId,
            WhatsAppBusinessAccountId = values.WhatsAppBusinessAccountId,
            BusinessPhoneNumberMasked = values.BusinessPhoneNumberMasked,
            CredentialReference = values.CredentialReference,
            GraphApiVersion = values.GraphApiVersion,
            IsActive = request.IsActive,
            IsProductionEnabled = false,
            WebhookConfigurationStatus = "Unknown",
            TemplateSyncStatus = "NotStarted",
            IsDemoSeeded = false
        };

        await whatsAppRepository.AddIntegrationAsync(integration, cancellationToken);

        try
        {
            await whatsAppRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsPhoneNumberIdUniqueViolation(exception))
        {
            return ApiResponse<WhatsAppIntegrationDetailResponse>.Fail("Phone number ID is already used by another integration.");
        }

        return ApiResponse<WhatsAppIntegrationDetailResponse>.Ok(MapDetail(integration), "WhatsApp integration created.");
    }

    public async Task<ApiResponse<WhatsAppIntegrationDetailResponse>> UpdateIntegrationAsync(Guid integrationId, WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return ApiResponse<WhatsAppIntegrationDetailResponse>.Fail(integrationResult.ErrorMessage);
        }

        if (!TryValidateConfiguration(request, out var values, out var validationErrors))
        {
            return ApiResponse<WhatsAppIntegrationDetailResponse>.Fail("WhatsApp integration configuration is invalid.", validationErrors);
        }

        var integration = integrationResult.Integration;
        integration.DisplayName = values.DisplayName;
        integration.PhoneNumberId = values.PhoneNumberId;
        integration.WhatsAppBusinessAccountId = values.WhatsAppBusinessAccountId;
        integration.BusinessPhoneNumberMasked = values.BusinessPhoneNumberMasked;
        integration.CredentialReference = values.CredentialReference;
        integration.GraphApiVersion = values.GraphApiVersion;
        integration.IsActive = request.IsActive;
        // A deliberate configuration edit permanently opts this integration out of demo reseeding.
        integration.IsDemoSeeded = false;

        try
        {
            await whatsAppRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsPhoneNumberIdUniqueViolation(exception))
        {
            return ApiResponse<WhatsAppIntegrationDetailResponse>.Fail("Phone number ID is already used by another integration.");
        }

        return ApiResponse<WhatsAppIntegrationDetailResponse>.Ok(MapDetail(integration), "WhatsApp integration updated.");
    }

    public async Task<ApiResponse<WhatsAppProductionEnableResponse>> EnableProductionAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail(integrationResult.ErrorMessage);
        }

        var integration = integrationResult.Integration;

        if (!cloudOptions.Value.Enabled)
        {
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail("WhatsApp Cloud integration is not enabled. Contact an administrator.");
        }

        if (!integration.IsActive)
        {
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail("Integration must be active before enabling production sending.");
        }

        if (string.IsNullOrWhiteSpace(integration.PhoneNumberId)
            || string.IsNullOrWhiteSpace(integration.WhatsAppBusinessAccountId)
            || string.IsNullOrWhiteSpace(integration.GraphApiVersion))
        {
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail("Phone number ID, WhatsApp Business Account ID, and Graph API version must be configured before enabling production sending.");
        }

        if (string.IsNullOrWhiteSpace(integration.CredentialReference))
        {
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail("A credential reference must be configured before enabling production sending.");
        }

        var credentials = await credentialResolver.ResolveAsync(integration, cancellationToken);
        if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail(credentials.FailureSummary ?? "WhatsApp credentials could not be resolved.");
        }

        // Reuses the existing health infrastructure (non-message provider validation call) instead
        // of duplicating connectivity checks here.
        var health = await healthService.CheckAsync(integration, cancellationToken);
        integration.LastHealthCheckAt = health.CheckedAt;
        integration.WebhookConfigurationStatus = health.Status;

        if (health.Status is not ("Healthy" or "ProductionPending"))
        {
            integration.LastErrorSummary = health.Message;
            await whatsAppRepository.SaveChangesAsync(cancellationToken);
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail($"Integration is not ready for production sending: {health.Message}");
        }

        integration.LastSuccessfulHealthCheckAt = health.CheckedAt;
        integration.LastErrorSummary = null;
        integration.IsProductionEnabled = true;
        await whatsAppRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<WhatsAppProductionEnableResponse>.Ok(new WhatsAppProductionEnableResponse
        {
            IntegrationId = integration.Id,
            IsProductionEnabled = true,
            Status = "Enabled",
            Message = "Production sending enabled.",
            CheckedAt = health.CheckedAt
        }, "WhatsApp production sending enabled.");
    }

    public async Task<ApiResponse<WhatsAppProductionEnableResponse>> DisableProductionAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return ApiResponse<WhatsAppProductionEnableResponse>.Fail(integrationResult.ErrorMessage);
        }

        var integration = integrationResult.Integration;
        integration.IsProductionEnabled = false;
        await whatsAppRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<WhatsAppProductionEnableResponse>.Ok(new WhatsAppProductionEnableResponse
        {
            IntegrationId = integration.Id,
            IsProductionEnabled = false,
            Status = "Disabled",
            Message = "Production sending disabled.",
            CheckedAt = DateTimeOffset.UtcNow
        }, "WhatsApp production sending disabled.");
    }

    public async Task<ApiResponse<WhatsAppIntegrationHealthResponse>> CheckHealthAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return ApiResponse<WhatsAppIntegrationHealthResponse>.Fail(integrationResult.ErrorMessage);
        }

        var integration = integrationResult.Integration;
        var result = await healthService.CheckAsync(integration, cancellationToken);

        integration.LastHealthCheckAt = result.CheckedAt;
        integration.WebhookConfigurationStatus = result.Status;

        if (result.Status == "Healthy")
        {
            integration.LastSuccessfulHealthCheckAt = result.CheckedAt;
            integration.LastErrorSummary = null;
        }
        else
        {
            integration.LastErrorSummary = result.Message;
        }

        await whatsAppRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<WhatsAppIntegrationHealthResponse>.Ok(result);
    }

    public async Task<ApiResponse<WhatsAppTemplateSyncResponse>> SyncTemplatesAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return ApiResponse<WhatsAppTemplateSyncResponse>.Fail(integrationResult.ErrorMessage);
        }

        var integration = integrationResult.Integration;
        if (!integration.IsActive)
        {
            return ApiResponse<WhatsAppTemplateSyncResponse>.Fail("Integration is inactive.");
        }

        var credentials = await credentialResolver.ResolveAsync(integration, cancellationToken);
        if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            if (hostEnvironment.IsDevelopment())
            {
                var fallbackResult = await SyncDevelopmentSeededTemplatesAsync(integration, cancellationToken);
                return ApiResponse<WhatsAppTemplateSyncResponse>.Ok(fallbackResult, "Template synchronization completed using development seeded templates.");
            }

            integration.TemplateSyncStatus = "Failed";
            integration.LastErrorSummary = credentials.FailureSummary;
            await whatsAppRepository.SaveChangesAsync(cancellationToken);
            return ApiResponse<WhatsAppTemplateSyncResponse>.Fail(credentials.FailureSummary ?? "Template sync failed.");
        }

        var provider = await whatsAppCloudClient.GetTemplatesAsync(new WhatsAppGetTemplatesRequest
        {
            CompanyId = integration.CompanyId,
            IntegrationId = integration.Id,
            AccessToken = credentials.AccessToken,
            GraphApiVersion = integration.GraphApiVersion,
            WhatsAppBusinessAccountId = integration.WhatsAppBusinessAccountId
        }, cancellationToken);

        if (!provider.Success)
        {
            integration.TemplateSyncStatus = "Failed";
            integration.LastErrorSummary = provider.FailureReason ?? "Template synchronization failed.";
            await whatsAppRepository.SaveChangesAsync(cancellationToken);
            return ApiResponse<WhatsAppTemplateSyncResponse>.Fail("Template synchronization failed.", [integration.LastErrorSummary ?? "Unknown provider failure."]);
        }

        var existing = (await whatsAppRepository.ListTemplatesForIntegrationAsync(integration.CompanyId, integration.Id, cancellationToken))
            .ToDictionary(template => $"{template.Name}|{template.LanguageCode}", StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var item in provider.Templates)
        {
            var key = $"{item.Name}|{item.LanguageCode}";
            if (existing.TryGetValue(key, out var current))
            {
                var changed = ApplyTemplate(current, item);
                current.LastSyncedAt = DateTimeOffset.UtcNow;
                current.IsActive = true;

                if (changed)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }

                existing.Remove(key);
                continue;
            }

            var template = new WhatsAppTemplate
            {
                Id = Guid.NewGuid(),
                CompanyId = integration.CompanyId,
                WhatsAppIntegrationId = integration.Id,
                ExternalTemplateId = item.ExternalTemplateId,
                Name = item.Name,
                LanguageCode = item.LanguageCode,
                Category = item.Category,
                Status = item.Status,
                HeaderType = item.HeaderType,
                BodyText = item.BodyText,
                FooterText = item.FooterText,
                VariableCount = item.Placeholders.Count,
                ComponentsJson = item.ComponentsJson,
                LastSyncedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };

            await whatsAppRepository.AddTemplateAsync(template, cancellationToken);
            added++;
        }

        var disabled = 0;
        foreach (var leftover in existing.Values)
        {
            if (!leftover.IsActive)
            {
                continue;
            }

            leftover.IsActive = false;
            leftover.LastSyncedAt = DateTimeOffset.UtcNow;
            disabled++;
        }

        integration.TemplateSyncStatus = "Completed";
        integration.LastTemplateSyncAt = DateTimeOffset.UtcNow;
        integration.LastErrorSummary = null;
        await whatsAppRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<WhatsAppTemplateSyncResponse>.Ok(new WhatsAppTemplateSyncResponse
        {
            Added = added,
            Updated = updated,
            Unchanged = unchanged,
            Disabled = disabled,
            Failed = 0,
            SyncedAt = integration.LastTemplateSyncAt ?? DateTimeOffset.UtcNow,
            Status = "Completed",
            Message = "Template synchronization completed."
        });
    }

    public async Task<ApiResponse<WhatsAppTemplateListResponse>> ListTemplatesAsync(Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return ApiResponse<WhatsAppTemplateListResponse>.Fail(integrationResult.ErrorMessage);
        }

        var integration = integrationResult.Integration;
        var paged = await whatsAppRepository.ListTemplatesAsync(integration.CompanyId, integration.Id, query, cancellationToken);
        var items = paged.Items.Select(MapSummary).ToList();

        return ApiResponse<WhatsAppTemplateListResponse>.Ok(new WhatsAppTemplateListResponse
        {
            Items = items,
            TotalCount = paged.TotalCount,
            Page = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalPages = paged.TotalPages
        });
    }

    public async Task<ApiResponse<WhatsAppTemplateDetailResponse>> GetTemplateAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken)
    {
        var templateResult = await GetTemplateForTenantAsync(integrationId, templateId, cancellationToken);
        if (!templateResult.Success || templateResult.Template is null)
        {
            return ApiResponse<WhatsAppTemplateDetailResponse>.Fail(templateResult.ErrorMessage);
        }

        return ApiResponse<WhatsAppTemplateDetailResponse>.Ok(MapDetail(templateResult.Template));
    }

    public async Task<ApiResponse<WhatsAppTemplatePreviewResponse>> PreviewTemplateAsync(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken)
    {
        var templateResult = await GetTemplateForTenantAsync(integrationId, templateId, cancellationToken);
        if (!templateResult.Success || templateResult.Template is null)
        {
            return ApiResponse<WhatsAppTemplatePreviewResponse>.Fail(templateResult.ErrorMessage);
        }

        var template = templateResult.Template;
        var validation = variableValidator.Validate(template, request.Variables);
        var variables = validation.SanitizedVariables.ToList();

        var bodyPreview = RenderTemplateText(template.BodyText, variables, includeMissingMarker: true, out var bodyMissing);
        var headerPreview = RenderTemplateText(template.HeaderType ?? string.Empty, variables, includeMissingMarker: true, out var headerMissing);
        var footerPreview = template.FooterText ?? string.Empty;

        return ApiResponse<WhatsAppTemplatePreviewResponse>.Ok(new WhatsAppTemplatePreviewResponse
        {
            HeaderPreview = headerPreview,
            BodyPreview = bodyPreview,
            FooterPreview = footerPreview,
            HasMissingVariables = bodyMissing || headerMissing
        });
    }

    public async Task<ApiResponse<ConversationMessageResponse>> SendTemplateMessageAsync(Guid conversationId, Guid templateId, SendWhatsAppTemplateMessageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var error))
        {
            return ApiResponse<ConversationMessageResponse>.Fail(error, [error]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation was not found.");
        }

        if (conversation.Channel != DTOs.ReservationContext.GuestChannel.WhatsApp)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Template messages are only available for WhatsApp conversations.");
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation state does not allow this message.");
        }

        if (!conversation.HumanTakeoverEnabled || !transitionPolicy.CanStoreMessage(conversation, ConversationSenderType.Host))
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Enable human takeover before sending a host reply.");
        }

        var integration = await whatsAppRepository.GetActiveIntegrationByCompanyIdAsync(companyId, cancellationToken);
        if (integration is null || !integration.IsActive)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("WhatsApp integration is not configured for this company.");
        }

        var template = await whatsAppRepository.GetTemplateForCompanyAsync(companyId, integration.Id, templateId, cancellationToken);
        if (template is null)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Template was not found.");
        }

        if (!template.IsActive || !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Only approved active templates can be sent.");
        }

        var validation = variableValidator.Validate(template, request.Variables);
        if (!validation.Success)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Template variable validation failed.", validation.Errors.ToList());
        }

        if (!phoneNumberNormalizer.TryNormalize(conversation.ChannelIdentity, out var normalizedRecipient))
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation channel identity is not a valid WhatsApp destination.");
        }

        await subscriptionEntitlementService.ConsumeQuotaAsync(
            companyId,
            UsageMetric.WhatsAppMessages,
            1,
            $"whatsapp:template-send:{conversation.Id:D}:{template.Id:D}:{request.LanguageCode?.Trim() ?? template.LanguageCode}",
            cancellationToken);

        var credentials = await credentialResolver.ResolveAsync(integration, cancellationToken);
        if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return ApiResponse<ConversationMessageResponse>.Fail(credentials.FailureSummary ?? "WhatsApp sending is unavailable.");
        }

        var rendered = RenderTemplateText(template.BodyText, validation.SanitizedVariables, includeMissingMarker: false, out _);

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversation.Id,
            SenderType = ConversationSenderType.Host,
            MessageType = ConversationMessageType.Text,
            Content = rendered,
            Provider = ConversationMessageProvider.WhatsAppCloud,
            DeliveryStatus = ConversationMessageDeliveryStatus.Pending,
            IsTemplateMessage = true,
            WhatsAppTemplateId = template.Id,
            TemplateName = template.Name,
            TemplateLanguageCode = request.LanguageCode?.Trim() ?? template.LanguageCode,
            TemplateRenderedPreview = rendered,
            SentAt = DateTimeOffset.UtcNow,
            IsInternal = false
        };

        conversation.LastActivityAt = message.SentAt;

        await conversationRepository.AddMessageAsync(message, cancellationToken);
        await conversationRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Conversation),
            EntityId = conversation.Id,
            Action = "WhatsAppTemplateMessageStored",
            Details = JsonSerializer.Serialize(new
            {
                template = template.Name,
                language = message.TemplateLanguageCode
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        await realtimePublisher.PublishMessageCreatedAsync(companyId, conversation.Id, new
        {
            conversationId = conversation.Id,
            message = MapMessage(message),
            isInternal = false,
            timestamp = DateTimeOffset.UtcNow
        }, false, cancellationToken);

        var sendResult = await whatsAppCloudClient.SendTemplateMessageAsync(new WhatsAppTemplateSendRequest
        {
            CompanyId = integration.CompanyId,
            IntegrationId = integration.Id,
            AccessToken = credentials.AccessToken,
            GraphApiVersion = integration.GraphApiVersion,
            PhoneNumberId = integration.PhoneNumberId,
            To = normalizedRecipient,
            TemplateName = template.Name,
            LanguageCode = message.TemplateLanguageCode ?? template.LanguageCode,
            Variables = validation.SanitizedVariables,
            ClientMessageId = request.ClientRequestId?.Trim() ?? message.Id.ToString("N")
        }, cancellationToken);

        if (sendResult.Success)
        {
            message.ExternalMessageId = sendResult.ExternalMessageId;
            message.ProviderRequestId = sendResult.ProviderRequestId;
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Sent;
            message.FailedAt = null;
            message.FailureCode = null;
            message.FailureReason = null;
            message.FailureCategory = null;
        }
        else
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = sendResult.FailureCode;
            var mapped = WhatsAppFailureMapper.Map(sendResult.FailureCode, sendResult.FailureReason, sendResult.HttpStatusCode, null, null, sendResult.IsTransientFailure, null);
            message.FailureCategory = mapped.Category;
            message.FailureReason = mapped.Summary;
            message.ProviderRequestId = sendResult.ProviderRequestId;
        }

        await conversationRepository.SaveChangesAsync(cancellationToken);

        await realtimePublisher.PublishMessageUpdatedAsync(companyId, conversation.Id, new
        {
            conversationId = conversation.Id,
            message = MapMessage(message),
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        return ApiResponse<ConversationMessageResponse>.Ok(MapMessage(message), "Template message processed.");
    }

    public async Task<ApiResponse<ConversationMessageResponse>> SendLifecycleAutomationTemplateMessageAsync(
        Guid companyId,
        Guid conversationId,
        Guid integrationId,
        Guid templateId,
        IReadOnlyCollection<string> variables,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var duplicate = await conversationRepository.FindByExternalMessageIdAsync(companyId, idempotencyKey, null, cancellationToken);
        if (duplicate is not null)
        {
            return ApiResponse<ConversationMessageResponse>.Ok(MapMessage(duplicate), "Lifecycle automation template message already exists.");
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation was not found.");
        }

        if (conversation.Channel != DTOs.ReservationContext.GuestChannel.WhatsApp)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Template messages are only available for WhatsApp conversations.");
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation state does not allow this message.");
        }

        var integration = await whatsAppRepository.GetActiveIntegrationByCompanyIdAsync(companyId, cancellationToken);
        if (integration is null || integration.Id != integrationId)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("WhatsApp integration is not configured for this company.");
        }

        // Ownership (company + integration + template) is enforced by this same lookup used
        // elsewhere; never trust a caller-supplied template without re-verifying it here.
        var template = await whatsAppRepository.GetTemplateForCompanyAsync(companyId, integrationId, templateId, cancellationToken);
        if (template is null)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Template was not found.");
        }

        if (!template.IsActive || !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Only approved active templates can be sent.");
        }

        var validation = variableValidator.Validate(template, variables);
        if (!validation.Success)
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Template variable validation failed.", validation.Errors.ToList());
        }

        if (!phoneNumberNormalizer.TryNormalize(conversation.ChannelIdentity, out var normalizedRecipient))
        {
            return ApiResponse<ConversationMessageResponse>.Fail("Conversation channel identity is not a valid WhatsApp destination.");
        }

        await subscriptionEntitlementService.ConsumeQuotaAsync(
            companyId,
            UsageMetric.WhatsAppMessages,
            1,
            $"whatsapp:lifecycle-template-send:{idempotencyKey}",
            cancellationToken);

        var credentials = await credentialResolver.ResolveAsync(integration, cancellationToken);
        if (!credentials.Success || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return ApiResponse<ConversationMessageResponse>.Fail(credentials.FailureSummary ?? "WhatsApp sending is unavailable.");
        }

        var rendered = RenderTemplateText(template.BodyText, validation.SanitizedVariables, includeMissingMarker: false, out _);

        var message = new ConversationMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversation.Id,
            SenderType = ConversationSenderType.System,
            MessageType = ConversationMessageType.LifecycleAutomation,
            Content = rendered,
            ExternalMessageId = idempotencyKey,
            Provider = ConversationMessageProvider.WhatsAppCloud,
            DeliveryStatus = ConversationMessageDeliveryStatus.Pending,
            IsTemplateMessage = true,
            WhatsAppTemplateId = template.Id,
            TemplateName = template.Name,
            TemplateLanguageCode = template.LanguageCode,
            TemplateRenderedPreview = rendered,
            SentAt = DateTimeOffset.UtcNow,
            IsInternal = false
        };

        conversation.LastActivityAt = message.SentAt;

        await conversationRepository.AddMessageAsync(message, cancellationToken);
        await conversationRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Conversation),
            EntityId = conversation.Id,
            Action = "LifecycleAutomationTemplateMessageStored",
            Details = JsonSerializer.Serialize(new
            {
                template = template.Name,
                language = template.LanguageCode
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        await realtimePublisher.PublishMessageCreatedAsync(companyId, conversation.Id, new
        {
            conversationId = conversation.Id,
            message = MapMessage(message),
            isInternal = false,
            timestamp = DateTimeOffset.UtcNow
        }, false, cancellationToken);

        var sendResult = await whatsAppCloudClient.SendTemplateMessageAsync(new WhatsAppTemplateSendRequest
        {
            CompanyId = integration.CompanyId,
            IntegrationId = integration.Id,
            AccessToken = credentials.AccessToken,
            GraphApiVersion = integration.GraphApiVersion,
            PhoneNumberId = integration.PhoneNumberId,
            To = normalizedRecipient,
            TemplateName = template.Name,
            LanguageCode = template.LanguageCode,
            Variables = validation.SanitizedVariables,
            ClientMessageId = idempotencyKey
        }, cancellationToken);

        if (sendResult.Success)
        {
            message.ExternalMessageId = sendResult.ExternalMessageId ?? idempotencyKey;
            message.ProviderRequestId = sendResult.ProviderRequestId;
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Sent;
            message.FailedAt = null;
            message.FailureCode = null;
            message.FailureReason = null;
            message.FailureCategory = null;
        }
        else
        {
            message.DeliveryStatus = ConversationMessageDeliveryStatus.Failed;
            message.FailedAt = DateTimeOffset.UtcNow;
            message.FailureCode = sendResult.FailureCode;
            var mapped = WhatsAppFailureMapper.Map(sendResult.FailureCode, sendResult.FailureReason, sendResult.HttpStatusCode, null, null, sendResult.IsTransientFailure, null);
            message.FailureCategory = mapped.Category;
            message.FailureReason = mapped.Summary;
            message.ProviderRequestId = sendResult.ProviderRequestId;
        }

        await conversationRepository.SaveChangesAsync(cancellationToken);

        await realtimePublisher.PublishMessageUpdatedAsync(companyId, conversation.Id, new
        {
            conversationId = conversation.Id,
            message = MapMessage(message),
            timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        return ApiResponse<ConversationMessageResponse>.Ok(MapMessage(message), "Lifecycle automation template message processed.");
    }

    public async Task<ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>> GetCustomerServiceWindowStatusAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var error))
        {
            return ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>.Fail(error, [error]);
        }

        var conversation = await conversationRepository.GetByIdForCompanyAsync(companyId, conversationId, cancellationToken);
        if (conversation is null)
        {
            return ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>.Fail("Conversation was not found.");
        }

        var evaluation = await windowEvaluator.EvaluateAsync(companyId, conversationId, cancellationToken);
        return ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>.Ok(new WhatsAppCustomerServiceWindowStatusResponse
        {
            IsOpen = evaluation.IsOpen,
            LastInboundAt = evaluation.LastInboundAt,
            ExpiresAt = evaluation.ExpiresAt,
            Reason = evaluation.Reason
        });
    }

    private async Task<(bool Success, string ErrorMessage, WhatsAppIntegration? Integration)> GetIntegrationAsync(Guid integrationId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var error))
        {
            return (false, error, null);
        }

        var integration = await whatsAppRepository.GetIntegrationForCompanyAsync(companyId, integrationId, cancellationToken);
        if (integration is null)
        {
            return (false, "WhatsApp integration was not found.", null);
        }

        return (true, string.Empty, integration);
    }

    private static WhatsAppIntegrationDetailResponse MapDetail(WhatsAppIntegration integration)
    {
        return new WhatsAppIntegrationDetailResponse
        {
            Id = integration.Id,
            DisplayName = integration.DisplayName,
            PhoneNumberId = integration.PhoneNumberId,
            WhatsAppBusinessAccountId = integration.WhatsAppBusinessAccountId,
            BusinessPhoneNumberMasked = integration.BusinessPhoneNumberMasked,
            CredentialReference = integration.CredentialReference,
            GraphApiVersion = integration.GraphApiVersion,
            IsActive = integration.IsActive,
            IsProductionEnabled = integration.IsProductionEnabled,
            Mode = integration.IsProductionEnabled ? "Production" : "Development",
            HealthStatus = string.IsNullOrWhiteSpace(integration.WebhookConfigurationStatus) ? "Unknown" : integration.WebhookConfigurationStatus,
            LastHealthCheckAt = integration.LastHealthCheckAt,
            LastSuccessfulHealthCheckAt = integration.LastSuccessfulHealthCheckAt,
            LastTemplateSyncAt = integration.LastTemplateSyncAt,
            LastErrorSummary = integration.LastErrorSummary
        };
    }

    private sealed record WhatsAppIntegrationConfigurationValues(
        string DisplayName,
        string PhoneNumberId,
        string WhatsAppBusinessAccountId,
        string BusinessPhoneNumberMasked,
        string? CredentialReference,
        string GraphApiVersion);

    private static bool TryValidateConfiguration(
        WhatsAppIntegrationConfigurationRequest request,
        out WhatsAppIntegrationConfigurationValues values,
        out IReadOnlyCollection<string> errors)
    {
        var errorList = new List<string>();

        var displayName = (request.DisplayName ?? string.Empty).Trim();
        var phoneNumberId = (request.PhoneNumberId ?? string.Empty).Trim();
        var wabaId = (request.WhatsAppBusinessAccountId ?? string.Empty).Trim();
        var maskedNumber = (request.BusinessPhoneNumberMasked ?? string.Empty).Trim();
        var graphApiVersion = (request.GraphApiVersion ?? string.Empty).Trim();
        var credentialReference = string.IsNullOrWhiteSpace(request.CredentialReference)
            ? null
            : request.CredentialReference.Trim();

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 120)
        {
            errorList.Add("Display name is required and must be 120 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumberId) || phoneNumberId.Length > 160)
        {
            errorList.Add("Phone number ID is required and must be 160 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(wabaId) || wabaId.Length > 160)
        {
            errorList.Add("WhatsApp Business Account ID is required and must be 160 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(maskedNumber) || maskedNumber.Length > 32)
        {
            errorList.Add("A masked business phone number display value is required and must be 32 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(graphApiVersion) || !GraphApiVersionPattern.IsMatch(graphApiVersion))
        {
            errorList.Add("Graph API version must match the expected format, for example 'v23.0'.");
        }

        if (credentialReference is not null && !CredentialReferencePattern.IsMatch(credentialReference))
        {
            errorList.Add("Credential reference must contain only letters, digits, underscores, or hyphens (max 64 characters).");
        }

        values = new WhatsAppIntegrationConfigurationValues(displayName, phoneNumberId, wabaId, maskedNumber, credentialReference, graphApiVersion);
        errors = errorList;
        return errorList.Count == 0;
    }

    private static bool IsPhoneNumberIdUniqueViolation(DbUpdateException dbUpdateException)
    {
        return dbUpdateException.GetBaseException() is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, PhoneNumberIdUniqueIndexName, StringComparison.Ordinal);
    }

    private async Task<(bool Success, string ErrorMessage, WhatsAppTemplate? Template)> GetTemplateForTenantAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken)
    {
        var integrationResult = await GetIntegrationAsync(integrationId, cancellationToken);
        if (!integrationResult.Success || integrationResult.Integration is null)
        {
            return (false, integrationResult.ErrorMessage, null);
        }

        var template = await whatsAppRepository.GetTemplateForCompanyAsync(integrationResult.Integration.CompanyId, integrationId, templateId, cancellationToken);
        if (template is null)
        {
            return (false, "Template was not found.", null);
        }

        return (true, string.Empty, template);
    }

    private static bool ApplyTemplate(WhatsAppTemplate current, WhatsAppProviderTemplate incoming)
    {
        var changed = false;

        changed |= Assign(current.ExternalTemplateId, incoming.ExternalTemplateId, value => current.ExternalTemplateId = value ?? string.Empty);
        changed |= Assign(current.Category, incoming.Category, value => current.Category = value ?? string.Empty);
        changed |= Assign(current.Status, incoming.Status, value => current.Status = value ?? string.Empty);
        changed |= Assign(current.HeaderType, incoming.HeaderType, value => current.HeaderType = value);
        changed |= Assign(current.BodyText, incoming.BodyText, value => current.BodyText = value ?? string.Empty);
        changed |= Assign(current.FooterText, incoming.FooterText, value => current.FooterText = value);
        changed |= Assign(current.ComponentsJson, incoming.ComponentsJson, value => current.ComponentsJson = value ?? "[]");

        var incomingVariableCount = incoming.Placeholders.Count;
        if (current.VariableCount != incomingVariableCount)
        {
            current.VariableCount = incomingVariableCount;
            changed = true;
        }

        return changed;
    }

    private async Task<WhatsAppTemplateSyncResponse> SyncDevelopmentSeededTemplatesAsync(WhatsAppIntegration integration, CancellationToken cancellationToken)
    {
        var seededTemplates = new[]
        {
            new
            {
                Name = "welcome_guest",
                LanguageCode = "en",
                Category = "UTILITY",
                Status = "APPROVED",
                HeaderType = (string?)"TEXT",
                BodyText = "Hello {{1}}, welcome to StayFlow. Your stay starts on {{2}}.",
                FooterText = (string?)"StayFlow Concierge",
                VariableCount = 2
            },
            new
            {
                Name = "booking_confirmation",
                LanguageCode = "fr",
                Category = "UTILITY",
                Status = "APPROVED",
                HeaderType = (string?)"TEXT",
                BodyText = "Bonjour {{1}}, votre reservation {{2}} est confirmee.",
                FooterText = (string?)"StayFlow Concierge",
                VariableCount = 2
            },
            new
            {
                Name = "late_checkout",
                LanguageCode = "en",
                Category = "MARKETING",
                Status = "PENDING",
                HeaderType = (string?)null,
                BodyText = "Late checkout request for {{1}} is under review.",
                FooterText = (string?)null,
                VariableCount = 1
            },
            new
            {
                Name = "checkin_instructions",
                LanguageCode = "es",
                Category = "AUTHENTICATION",
                Status = "APPROVED",
                HeaderType = (string?)"TEXT",
                BodyText = "Hola {{1}}, usa el codigo {{2}} para el check-in.",
                FooterText = (string?)"StayFlow Concierge",
                VariableCount = 2
            },
            new
            {
                Name = "checkout_reminder",
                LanguageCode = "en",
                Category = "UTILITY",
                Status = "REJECTED",
                HeaderType = (string?)null,
                BodyText = "Reminder: checkout is at {{1}}.",
                FooterText = (string?)null,
                VariableCount = 1
            }
        };

        logger.LogInformation(
            "Using development WhatsApp template fallback for integration {IntegrationId} in company {CompanyId}.",
            integration.Id,
            integration.CompanyId);

        var existing = (await whatsAppRepository.ListTemplatesForIntegrationAsync(integration.CompanyId, integration.Id, cancellationToken))
            .ToDictionary(template => $"{template.Name}|{template.LanguageCode}", StringComparer.OrdinalIgnoreCase);

        var added = 0;
        var updated = 0;
        var unchanged = 0;

        foreach (var item in seededTemplates)
        {
            var key = $"{item.Name}|{item.LanguageCode}";
            if (existing.TryGetValue(key, out var current))
            {
                var changed = false;

                changed |= Assign(current.ExternalTemplateId, $"dev-seeded-{item.Name}-{item.LanguageCode}", value => current.ExternalTemplateId = value ?? string.Empty);
                changed |= Assign(current.Category, item.Category, value => current.Category = value ?? string.Empty);
                changed |= Assign(current.Status, item.Status, value => current.Status = value ?? string.Empty);
                changed |= Assign(current.HeaderType, item.HeaderType, value => current.HeaderType = value);
                changed |= Assign(current.BodyText, item.BodyText, value => current.BodyText = value ?? string.Empty);
                changed |= Assign(current.FooterText, item.FooterText, value => current.FooterText = value);
                changed |= Assign(current.ComponentsJson, "{\"source\":\"development-seed\"}", value => current.ComponentsJson = value ?? "[]");

                if (current.VariableCount != item.VariableCount)
                {
                    current.VariableCount = item.VariableCount;
                    changed = true;
                }

                if (!current.IsActive)
                {
                    current.IsActive = true;
                    changed = true;
                }

                current.LastSyncedAt = DateTimeOffset.UtcNow;

                if (changed)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }

                existing.Remove(key);
                continue;
            }

            var template = new WhatsAppTemplate
            {
                Id = Guid.NewGuid(),
                CompanyId = integration.CompanyId,
                WhatsAppIntegrationId = integration.Id,
                ExternalTemplateId = $"dev-seeded-{item.Name}-{item.LanguageCode}",
                Name = item.Name,
                LanguageCode = item.LanguageCode,
                Category = item.Category,
                Status = item.Status,
                HeaderType = item.HeaderType,
                BodyText = item.BodyText,
                FooterText = item.FooterText,
                VariableCount = item.VariableCount,
                ComponentsJson = "{\"source\":\"development-seed\"}",
                LastSyncedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };

            await whatsAppRepository.AddTemplateAsync(template, cancellationToken);
            added++;
        }

        var disabled = 0;
        foreach (var leftover in existing.Values)
        {
            if (!leftover.IsActive)
            {
                continue;
            }

            leftover.IsActive = false;
            leftover.LastSyncedAt = DateTimeOffset.UtcNow;
            disabled++;
        }

        integration.TemplateSyncStatus = "Completed";
        integration.LastTemplateSyncAt = DateTimeOffset.UtcNow;
        integration.LastErrorSummary = null;
        await whatsAppRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Development template sync completed for integration {IntegrationId}: added={Added}, updated={Updated}, unchanged={Unchanged}, disabled={Disabled}.",
            integration.Id,
            added,
            updated,
            unchanged,
            disabled);

        return new WhatsAppTemplateSyncResponse
        {
            Added = added,
            Updated = updated,
            Unchanged = unchanged,
            Disabled = disabled,
            Failed = 0,
            SyncedAt = integration.LastTemplateSyncAt ?? DateTimeOffset.UtcNow,
            Status = "Completed",
            Message = "Template synchronization completed."
        };
    }

    private static bool Assign(string? current, string? incoming, Action<string?> assign)
    {
        if (string.Equals(current, incoming, StringComparison.Ordinal))
        {
            return false;
        }

        assign(incoming);
        return true;
    }

    private static string RenderTemplateText(string templateText, IReadOnlyCollection<string> variables, bool includeMissingMarker, out bool hasMissing)
    {
        var missing = false;
        var ordered = variables.ToList();

        var rendered = Regex.Replace(templateText, "\\{\\{(\\d+)\\}\\}", match =>
        {
            if (!int.TryParse(match.Groups[1].Value, out var index) || index <= 0)
            {
                return match.Value;
            }

            var actualIndex = index - 1;
            if (actualIndex >= ordered.Count)
            {
                missing = true;
                return includeMissingMarker ? $"[missing:{index}]" : string.Empty;
            }

            return ordered[actualIndex];
        });

        hasMissing = missing;
        return rendered;
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        if (!currentTenantContext.IsAuthenticated || currentTenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        companyId = tenantCompanyId;
        error = string.Empty;
        return true;
    }

    private static WhatsAppTemplateSummaryResponse MapSummary(WhatsAppTemplate template)
    {
        return new WhatsAppTemplateSummaryResponse
        {
            Id = template.Id,
            Name = template.Name,
            LanguageCode = template.LanguageCode,
            Category = template.Category,
            Status = template.Status,
            IsActive = template.IsActive,
            IsApproved = string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase),
            VariableCount = template.VariableCount,
            LastSyncedAt = template.LastSyncedAt
        };
    }

    private static WhatsAppTemplateDetailResponse MapDetail(WhatsAppTemplate template)
    {
        var variables = Enumerable.Range(1, template.VariableCount)
            .Select(index => new WhatsAppTemplateVariableDefinition
            {
                Position = index,
                Placeholder = $"{{{{{index}}}}}"
            })
            .ToList();

        return new WhatsAppTemplateDetailResponse
        {
            Id = template.Id,
            Name = template.Name,
            LanguageCode = template.LanguageCode,
            Category = template.Category,
            Status = template.Status,
            IsActive = template.IsActive,
            IsApproved = string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase),
            VariableCount = template.VariableCount,
            LastSyncedAt = template.LastSyncedAt,
            HeaderType = template.HeaderType,
            BodyText = template.BodyText,
            FooterText = template.FooterText,
            Variables = variables
        };
    }

    private static ConversationMessageResponse MapMessage(ConversationMessage message)
    {
        var safeFailureSummary = message.DeliveryStatus == ConversationMessageDeliveryStatus.Failed
            ? WhatsAppFailureMapper.Map(message.FailureCode, message.FailureReason).Summary
            : null;

        return new ConversationMessageResponse
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType,
            MessageType = message.MessageType,
            Content = message.Content,
            IsInternal = message.IsInternal,
            Provider = message.Provider,
            DeliveryStatus = message.DeliveryStatus,
            DeliveredAt = message.DeliveredAt,
            ReadAt = message.ReadAt,
            FailedAt = message.FailedAt,
            SafeFailureSummary = safeFailureSummary,
            RetryOfMessageId = message.RetryOfMessageId,
            SendAttemptNumber = message.SendAttemptNumber,
            CanRetry = message.DeliveryStatus == ConversationMessageDeliveryStatus.Failed
                && !message.IsInternal
                && message.Provider == ConversationMessageProvider.WhatsAppCloud
                && message.SenderType is ConversationSenderType.Host or ConversationSenderType.AI,
            IsTemplateMessage = message.IsTemplateMessage,
            WhatsAppTemplateId = message.WhatsAppTemplateId,
            TemplateName = message.TemplateName,
            TemplateLanguageCode = message.TemplateLanguageCode,
            TemplateRenderedPreview = message.TemplateRenderedPreview,
            SentAt = message.SentAt
        };
    }
}
