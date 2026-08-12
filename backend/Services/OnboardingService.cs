using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Onboarding;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.DTOs.PropertyKnowledge;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class OnboardingService(
    ApplicationDbContext dbContext,
    ICurrentTenantContext tenantContext,
    IPropertyService propertyService,
    IOrganizationInvitationService invitationService,
    IPropertyKnowledgeService propertyKnowledgeService,
    IWhatsAppTemplateService whatsAppTemplateService,
    ISubscriptionEntitlementService subscriptionEntitlementService,
    IOptions<AIProviderOptions> aiProviderOptions,
    IOptions<OpenAIOptions> openAiOptions,
    IHostEnvironment hostEnvironment) : IOnboardingService
{
    private static readonly OnboardingStep[] WorkflowSteps =
    [
        OnboardingStep.Welcome,
        OnboardingStep.OrganizationProfile,
        OnboardingStep.PlanConfirmation,
        OnboardingStep.FirstProperty,
        OnboardingStep.TeamInvitations,
        OnboardingStep.WhatsAppSetup,
        OnboardingStep.AiProviderSetup,
        OnboardingStep.KnowledgeBaseSetup,
        OnboardingStep.DemoData,
        OnboardingStep.Review
    ];

    private static readonly HashSet<OnboardingStep> OptionalSteps =
    [
        OnboardingStep.TeamInvitations,
        OnboardingStep.WhatsAppSetup,
        OnboardingStep.DemoData
    ];

    public async Task<ApiResponse<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        // Onboarding completion is an organization-level fact, so members without their own
        // progress row must still observe the completed state of the active organization.
        var progress = await FindProgressAsync(companyId, userId, cancellationToken)
            ?? await FindCompletedCompanyProgressAsync(companyId, cancellationToken);
        if (progress is null)
        {
            var notStarted = await BuildNotStartedStatusAsync(companyId, userId, cancellationToken);
            await AddOnboardingEventAsync(companyId, userId, "onboarding.step_viewed", notStarted.CurrentStep, notStarted.CurrentStepState, null, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApiResponse<OnboardingStatusDto>.Ok(notStarted);
        }

        var status = await BuildStatusAsync(progress, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.step_viewed", status.CurrentStep, status.CurrentStepState, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(status);
    }

    public async Task<ApiResponse<OnboardingStatusDto>> StartAsync(CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await FindProgressAsync(companyId, userId, cancellationToken);
        if (progress is null)
        {
            progress = new OnboardingProgress
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = userId,
                CurrentStep = OnboardingStep.Welcome.ToStorageValue(),
                StartedAtUtc = DateTimeOffset.UtcNow,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                IsCompleted = false,
                Version = 1
            };

            await dbContext.OnboardingProgressRecords.AddAsync(progress, cancellationToken);
            await AddAuditLogAsync(companyId, progress.Id, "OnboardingStarted", null, cancellationToken);
            await AddOnboardingEventAsync(companyId, userId, "onboarding.started", progress.CurrentStep, OnboardingStepState.InProgress.ToString(), null, cancellationToken);
        }
        else
        {
            await AddOnboardingEventAsync(companyId, userId, "onboarding.resumed", progress.CurrentStep, OnboardingStepState.InProgress.ToString(), null, cancellationToken);
        }

        if (!progress.IsCompleted)
        {
            var completed = ParseSteps(progress.CompletedStepsCsv).ToHashSet();
            var skipped = ParseSteps(progress.SkippedStepsCsv).ToHashSet();

            // Starting onboarding should move the workflow past Welcome exactly once.
            if (!completed.Contains(OnboardingStep.Welcome))
            {
                completed.Add(OnboardingStep.Welcome);
                skipped.Remove(OnboardingStep.Welcome);
                progress.CompletedStepsCsv = ToCsv(completed);
                progress.SkippedStepsCsv = ToCsv(skipped);
                await AddOnboardingEventAsync(
                    companyId,
                    userId,
                    "onboarding.step_completed",
                    OnboardingStep.Welcome.ToStorageValue(),
                    OnboardingStepState.Completed.ToString(),
                    null,
                    cancellationToken);
            }

            var completedOrSkipped = completed.Union(skipped).ToHashSet();
            var blockers = await CalculateBlockersAsync(progress, completedOrSkipped, cancellationToken);
            progress.CurrentStep = ResolveCurrentStep(progress, completedOrSkipped, blockers).ToStorageValue();
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is not null)
        {
            company.OnboardingState = progress.IsCompleted
                ? OnboardingStep.Completed.ToStorageValue()
                : progress.CurrentStep;
        }

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteOrganizationStepAsync(OnboardingOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Organization name is required.");
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.OrganizationProfile))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Organization profile step is not available yet.");
        }

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Organization was not found.");
        }

        var targetSlug = string.IsNullOrWhiteSpace(request.Slug)
            ? company.Slug
            : Slugify(request.Slug);
        if (string.IsNullOrWhiteSpace(targetSlug))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Organization slug is invalid.");
        }

        var normalizedSlug = targetSlug.ToUpperInvariant();
        var slugExists = await dbContext.Companies.AsNoTracking().AnyAsync(
            item => item.Id != company.Id && item.NormalizedSlug == normalizedSlug,
            cancellationToken);
        if (slugExists)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Organization slug already exists.");
        }

        company.Name = request.Name.Trim();
        company.Slug = targetSlug;
        company.NormalizedSlug = normalizedSlug;
        company.BrandingLogoUrl = NormalizeOptional(request.BrandingLogoUrl);
        company.BrandingPrimaryColor = NormalizeOptional(request.BrandingPrimaryColor);
        company.Email = NormalizeOptional(request.SupportContactEmail) ?? company.Email;
        company.TimeZone = NormalizeOptional(request.TimeZone) ?? company.TimeZone;

        CompleteStep(progress, OnboardingStep.OrganizationProfile);
        company.OnboardingState = progress.CurrentStep;

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingOrganizationCompleted", new
        {
            locale = NormalizeOptional(request.Locale),
            supportContactUpdated = !string.IsNullOrWhiteSpace(request.SupportContactEmail)
        }, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.OrganizationProfile.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompletePlanStepAsync(OnboardingPlanRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.PlanConfirmation))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Plan confirmation step is not available yet.");
        }

        var snapshot = await subscriptionEntitlementService.GetCurrentSnapshotAsync(companyId, cancellationToken);
        var effectivePlanName = NormalizeOptional(snapshot.PlanDisplayName) ?? snapshot.PlanName;

        if (!string.IsNullOrWhiteSpace(request.PlanName)
            && !string.Equals(effectivePlanName, request.PlanName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<OnboardingStatusDto>.Fail($"Plan changes must be completed through the billing flow. Current trusted plan is '{effectivePlanName}'.");
        }

        progress.SelectedPlanName = effectivePlanName;
        CompleteStep(progress, OnboardingStep.PlanConfirmation);

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingPlanConfirmed", new { planName = effectivePlanName }, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.PlanConfirmation.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompletePropertyStepAsync(OnboardingPropertyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.FirstProperty))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("First property step is not available yet.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.AddressLine1) || string.IsNullOrWhiteSpace(request.City))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Property name, address line 1, and city are required.");
        }

        var normalizedName = request.Name.Trim().ToUpperInvariant();
        var normalizedAddress = request.AddressLine1.Trim().ToUpperInvariant();
        var normalizedCity = request.City.Trim().ToUpperInvariant();
        var normalizedTimeZone = request.TimeZone.Trim().ToUpperInvariant();

        var existing = await dbContext.Properties
            .AsNoTracking()
            .Where(item => item.CompanyId == companyId && !item.IsDeleted)
            .FirstOrDefaultAsync(item =>
                item.Name.ToUpper() == normalizedName
                && item.AddressLine1.ToUpper() == normalizedAddress
                && item.City.ToUpper() == normalizedCity
                && item.TimeZone.ToUpper() == normalizedTimeZone,
                cancellationToken);

        Guid propertyId;
        if (existing is not null)
        {
            propertyId = existing.Id;
        }
        else
        {
            var created = await propertyService.CreateAsync(new CreatePropertyRequest
            {
                Name = request.Name,
                AddressLine1 = request.AddressLine1,
                AddressLine2 = request.AddressLine2,
                City = request.City,
                CountryCode = request.CountryCode,
                TimeZone = request.TimeZone,
                Description = request.Description
            }, cancellationToken);

            if (!created.Success || created.Data is null)
            {
                return ApiResponse<OnboardingStatusDto>.Fail(created.Message, created.Errors);
            }

            propertyId = created.Data.Id;
        }

        progress.FirstPropertyId = propertyId;
        CompleteStep(progress, OnboardingStep.FirstProperty);

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingPropertyCreated", new
        {
            progress.FirstPropertyId,
            request.IdempotencyKey
        }, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.FirstProperty.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    public async Task<ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>> CompleteInvitationsStepAsync(OnboardingInvitationsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.TeamInvitations))
        {
            return ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>.Fail("Team invitation step is not available yet.");
        }

        if (request.Invitations.Count == 0)
        {
            return ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>.Fail("At least one invitation is required, or skip the step.");
        }

        var results = new List<OnboardingInvitationResultDto>();
        foreach (var invitation in request.Invitations
                     .Where(item => !string.IsNullOrWhiteSpace(item.Email))
                     .GroupBy(item => item.Email.Trim().ToUpperInvariant())
                     .Select(group => group.First()))
        {
            var response = await invitationService.CreateAsync(new CreateOrganizationInvitationRequest
            {
                Email = invitation.Email,
                Role = invitation.Role
            }, cancellationToken);

            results.Add(new OnboardingInvitationResultDto
            {
                Email = invitation.Email,
                Role = invitation.Role,
                Success = response.Success,
                Message = response.Message
            });
        }

        var hasSuccess = results.Any(item => item.Success);
        var hasExistingActiveInvites = await dbContext.OrganizationInvitations
            .AsNoTracking()
            .AnyAsync(item => item.CompanyId == companyId
                && item.AcceptedAtUtc == null
                && item.RevokedAtUtc == null
                && item.ExpiresAtUtc > DateTimeOffset.UtcNow, cancellationToken);

        if (!hasSuccess && !hasExistingActiveInvites)
        {
            return ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>.Fail("No invitation was accepted for processing.");
        }

        CompleteStep(progress, OnboardingStep.TeamInvitations);

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingInvitationsSent", new
        {
            attempted = results.Count,
            successful = results.Count(item => item.Success)
        }, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.TeamInvitations.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);

        var status = await BuildStatusAsync(progress, cancellationToken);
        return ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>.Ok(new OnboardingActionResponse<OnboardingInvitationsResponse>
        {
            Status = status,
            Result = new OnboardingInvitationsResponse { Results = results }
        });
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteWhatsAppStepAsync(OnboardingWhatsAppRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.WhatsAppSetup))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("WhatsApp setup step is not available yet.");
        }

        var integrations = await whatsAppTemplateService.GetIntegrationsAsync(cancellationToken);
        if (!integrations.Success || integrations.Data is null || integrations.Data.Count == 0)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("WhatsApp integration is not configured for this tenant.");
        }

        var integration = request.IntegrationId.HasValue
            ? integrations.Data.FirstOrDefault(item => item.Id == request.IntegrationId.Value)
            : integrations.Data.First();
        if (integration is null)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Selected WhatsApp integration was not found.");
        }

        string status;
        if (request.RunHealthCheck)
        {
            var health = await whatsAppTemplateService.CheckHealthAsync(integration.Id, cancellationToken);
            if (!health.Success || health.Data is null)
            {
                return ApiResponse<OnboardingStatusDto>.Fail("WhatsApp health check failed. Resolve integration blockers first.");
            }

            status = health.Data.Status;
        }
        else
        {
            status = integration.HealthStatus;
        }

        if (!string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("WhatsApp integration is not healthy yet.");
        }

        CompleteStep(progress, OnboardingStep.WhatsAppSetup);

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingWhatsAppConfigured", new { integration.Id }, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.WhatsAppSetup.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteAiProviderStepAsync(OnboardingAiProviderRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.AiProviderSetup))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("AI provider setup step is not available yet.");
        }

        var provider = aiProviderOptions.Value.Provider;
        if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey) || string.IsNullOrWhiteSpace(openAiOptions.Value.Model))
            {
                return ApiResponse<OnboardingStatusDto>.Fail("OpenAI provider configuration is incomplete.");
            }

            CompleteStep(progress, OnboardingStep.AiProviderSetup);
        }
        else
        {
            if (!request.AcknowledgeDeterministicFallback && !request.SkipIfDeterministicOnly)
            {
                return ApiResponse<OnboardingStatusDto>.Fail("Confirm deterministic fallback readiness before continuing.");
            }

            if (request.SkipIfDeterministicOnly)
            {
                SkipStep(progress, OnboardingStep.AiProviderSetup);
                await AddOnboardingEventAsync(companyId, userId, "onboarding.step_skipped", OnboardingStep.AiProviderSetup.ToStorageValue(), OnboardingStepState.Skipped.ToString(), null, cancellationToken);
            }
            else
            {
                CompleteStep(progress, OnboardingStep.AiProviderSetup);
                await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.AiProviderSetup.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);
            }
        }

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingAiConfigured", new { provider }, cancellationToken);

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteKnowledgeStepAsync(OnboardingKnowledgeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.KnowledgeBaseSetup))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Knowledge setup step is not available yet.");
        }

        var propertyId = request.PropertyId ?? progress.FirstPropertyId;
        if (propertyId is null || propertyId == Guid.Empty)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("A valid property is required before knowledge setup.");
        }

        var property = await dbContext.Properties
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Id == propertyId.Value && !item.IsDeleted, cancellationToken);
        if (property is null)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("The selected property does not belong to this tenant.");
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Knowledge title and content are required.");
        }

        Guid? createdArticleId = null;
        var transactionCompleted = false;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        async Task RollbackIfPendingAsync()
        {
            if (transactionCompleted)
            {
                return;
            }

            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch
            {
                // Preserve the original failure; rollback best-effort should not hide it.
            }
            finally
            {
                transactionCompleted = true;
            }
        }

        try
        {
            var existing = await dbContext.PropertyKnowledgeArticles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == companyId
                    && item.PropertyId == propertyId
                    && !item.IsDeleted
                    && item.Title == request.Title.Trim()
                    && item.Content == request.Content.Trim(), cancellationToken);

            if (existing is null)
            {
                var created = await propertyKnowledgeService.CreateAsync(propertyId.Value, new CreatePropertyKnowledgeRequest
                {
                    Category = PropertyKnowledgeCategory.Other,
                    Title = request.Title,
                    Summary = request.Summary,
                    Content = request.Content,
                    Tags = request.Tags,
                    IsActive = true,
                    Priority = 0
                }, cancellationToken);

                if (!created.Success || created.Data is null)
                {
                    await RollbackIfPendingAsync();
                    return ApiResponse<OnboardingStatusDto>.Fail(created.Message, created.Errors);
                }

                createdArticleId = created.Data.Id;
                if (!created.Data.IsApproved)
                {
                    var approved = await propertyKnowledgeService.ApproveAsync(propertyId.Value, created.Data.Id, cancellationToken);
                    if (!approved.Success || approved.Data is null)
                    {
                        await CleanupCreatedKnowledgeArticleAsync(companyId, createdArticleId.Value, cancellationToken);
                        await RollbackIfPendingAsync();
                        return ApiResponse<OnboardingStatusDto>.Fail(approved.Message, approved.Errors);
                    }
                }
            }

            CompleteStep(progress, OnboardingStep.KnowledgeBaseSetup);

            await AddAuditLogAsync(companyId, progress.Id, "OnboardingKnowledgeAdded", new
            {
                propertyId,
                request.IdempotencyKey
            }, cancellationToken);
            await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.KnowledgeBaseSetup.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);

            progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            progress.Version++;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            transactionCompleted = true;
            return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
        }
        catch (Exception ex)
        {
            if (createdArticleId.HasValue)
            {
                try
                {
                    await CleanupCreatedKnowledgeArticleAsync(companyId, createdArticleId.Value, cancellationToken);
                }
                catch
                {
                    // Cleanup is best-effort; preserve the original exception for diagnostics.
                }
            }

            await RollbackIfPendingAsync();
            return ApiResponse<OnboardingStatusDto>.Fail($"Knowledge setup failed: {ex.Message}", [ex.Message]);
        }
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteDemoDataStepAsync(OnboardingDemoDataRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        if (hostEnvironment.IsProduction())
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Demo data generation is blocked in production.");
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, OnboardingStep.DemoData))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Demo data step is not available yet.");
        }

        var propertyId = progress.FirstPropertyId;
        if (propertyId is null || propertyId == Guid.Empty)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("First property must be configured before demo data.");
        }

        var property = await dbContext.Properties.AsNoTracking().FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Id == propertyId.Value && !item.IsDeleted, cancellationToken);
        if (property is null)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("The selected onboarding property does not belong to this tenant.");
        }

        var marker = $"[DEMO][ONBOARDING][{companyId:N}]";

        var createdGuests = new List<Guest>();
        var createdReservations = new List<Reservation>();
        var createdConversations = new List<Conversation>();
        var createdMessages = new List<ConversationMessage>();
        var createdKnowledgeItems = new List<PropertyKnowledgeArticle>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            Guest? guest = null;
            if (request.CreateSampleReservation || request.CreateSampleConversation)
            {
                guest = await dbContext.Guests.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Email == $"demo+{companyId:N}@stayflow.invalid" && !item.IsDeleted, cancellationToken);
                if (guest is null)
                {
                    guest = new Guest
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        FirstName = "Demo",
                        LastName = "Guest",
                        Email = $"demo+{companyId:N}@stayflow.invalid",
                        PreferredLanguage = "en",
                        CountryCode = "KE",
                        IsActive = true,
                        Notes = marker
                    };

                    await dbContext.Guests.AddAsync(guest, cancellationToken);
                    createdGuests.Add(guest);
                }
            }

            Reservation? reservation = null;
            if (request.CreateSampleReservation && guest is not null)
            {
                var externalRef = $"{marker}:reservation";
                reservation = await dbContext.Reservations.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.ExternalReservationReference == externalRef && !item.IsDeleted, cancellationToken);
                if (reservation is null)
                {
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    reservation = new Reservation
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        PropertyId = propertyId.Value,
                        PrimaryGuestId = guest.Id,
                        ExternalReservationReference = externalRef,
                        ReservationSource = "Demo",
                        CheckInDate = today,
                        CheckOutDate = today.AddDays(2),
                        Adults = 2,
                        Children = 0,
                        TotalGuestCount = 2,
                        Status = ReservationStatus.Confirmed,
                        IsActive = true,
                        InternalNotes = marker
                    };

                    await dbContext.Reservations.AddAsync(reservation, cancellationToken);
                    createdReservations.Add(reservation);
                }
            }

            Conversation? conversation = null;
            if (request.CreateSampleConversation && guest is not null)
            {
                var subject = $"{marker} First guest conversation";
                conversation = await dbContext.Conversations.FirstOrDefaultAsync(item => item.CompanyId == companyId && item.Subject == subject && !item.IsDeleted, cancellationToken);
                if (conversation is null)
                {
                    conversation = new Conversation
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        GuestId = guest.Id,
                        PropertyId = propertyId,
                        ReservationId = reservation?.Id,
                        Subject = subject,
                        Channel = DTOs.ReservationContext.GuestChannel.Web,
                        Status = ConversationStatus.Open,
                        StartedAt = DateTimeOffset.UtcNow,
                        LastActivityAt = DateTimeOffset.UtcNow,
                        HumanTakeoverEnabled = true
                    };

                    await dbContext.Conversations.AddAsync(conversation, cancellationToken);
                    createdConversations.Add(conversation);
                }

                var hasDemoMessage = await dbContext.ConversationMessages
                    .AnyAsync(item => item.CompanyId == companyId
                        && item.ConversationId == conversation.Id
                        && item.Content.Contains(marker), cancellationToken);
                if (!hasDemoMessage)
                {
                    var message = new ConversationMessage
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        ConversationId = conversation.Id,
                        SenderType = ConversationSenderType.Guest,
                        Content = $"{marker} Hello, can I check in early?",
                        MessageType = ConversationMessageType.Text,
                        Provider = ConversationMessageProvider.None,
                        SentAt = DateTimeOffset.UtcNow,
                        IsInternal = false
                    };

                    await dbContext.ConversationMessages.AddAsync(message, cancellationToken);
                    createdMessages.Add(message);
                }
            }

            if (request.CreateSampleKnowledge)
            {
                var knowledgeTitle = $"{marker} Welcome Instructions";
                var knowledgeExists = await dbContext.PropertyKnowledgeArticles.AnyAsync(item =>
                    item.CompanyId == companyId
                    && item.PropertyId == propertyId
                    && !item.IsDeleted
                    && item.Title == knowledgeTitle, cancellationToken);
                if (!knowledgeExists)
                {
                    var knowledgeItem = new PropertyKnowledgeArticle
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = companyId,
                        PropertyId = propertyId.Value,
                        Category = PropertyKnowledgeCategory.CheckIn,
                        Title = knowledgeTitle,
                        Content = "Guest check-in starts at 3 PM. Use self-check-in lock instructions in your reservation message.",
                        Tags = "demo,onboarding",
                        IsApproved = true,
                        IsActive = true,
                        ApprovedAt = DateTimeOffset.UtcNow,
                        Summary = marker,
                        Priority = 0
                    };

                    await dbContext.PropertyKnowledgeArticles.AddAsync(knowledgeItem, cancellationToken);
                    createdKnowledgeItems.Add(knowledgeItem);
                }
            }

            CompleteStep(progress, OnboardingStep.DemoData);

            await AddAuditLogAsync(companyId, progress.Id, "OnboardingDemoDataCreated", new
            {
                request.IdempotencyKey,
                environment = hostEnvironment.EnvironmentName
            }, cancellationToken);
            await AddOnboardingEventAsync(companyId, userId, "onboarding.step_completed", OnboardingStep.DemoData.ToStorageValue(), OnboardingStepState.Completed.ToString(), null, cancellationToken);

            progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            progress.Version++;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
        }
        catch (Exception ex)
        {
            await CleanupCreatedDemoDataAsync(createdGuests, createdReservations, createdConversations, createdMessages, createdKnowledgeItems, cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return ApiResponse<OnboardingStatusDto>.Fail($"Demo data generation failed: {ex.Message}", [ex.Message]);
        }
    }

    public async Task<ApiResponse<OnboardingStatusDto>> SkipStepAsync(string step, OnboardingSkipStepRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        if (!OnboardingStepExtensions.TryParse(step, out var targetStep))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Unknown onboarding step.");
        }

        if (!OptionalSteps.Contains(targetStep))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("This step cannot be skipped.");
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanActOnStep(progress, targetStep))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("This step cannot be skipped at the current onboarding stage.");
        }

        SkipStep(progress, targetStep);

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingStepSkipped", new
        {
            step = targetStep.ToStorageValue(),
            reason = NormalizeOptional(request.Reason)
        }, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.step_skipped", targetStep.ToStorageValue(), OnboardingStepState.Skipped.ToString(), null, cancellationToken);

        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteOnboardingAsync(OnboardingCompleteRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        if (!request.ConfirmChecklistReviewed)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Review confirmation is required before completion.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
            if (!progress.IsCompleted)
            {
                CompleteStep(progress, OnboardingStep.Review);
            }

            var status = await BuildStatusAsync(progress, cancellationToken);

            var blockingItems = status.Checklist.Where(item =>
                !item.Optional
                && !string.Equals(item.Status, "complete", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (blockingItems.Count > 0 || status.Blockers.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ApiResponse<OnboardingStatusDto>.Fail("Onboarding completion is blocked by remaining required setup.");
            }

            progress.IsCompleted = true;
            progress.CompletedAtUtc = DateTimeOffset.UtcNow;
            progress.CompletedByUserId = userId;
            progress.CurrentStep = OnboardingStep.Completed.ToStorageValue();
            progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            progress.Version++;

            var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
            if (company is not null)
            {
                company.OnboardingState = OnboardingStep.Completed.ToStorageValue();
            }

            await AddAuditLogAsync(companyId, progress.Id, "OnboardingCompleted", new
            {
                elapsedMinutes = (int)Math.Max(0, (progress.CompletedAtUtc.Value - progress.StartedAtUtc).TotalMinutes)
            }, cancellationToken);
            await AddOnboardingEventAsync(companyId, userId, "onboarding.completed", OnboardingStep.Completed.ToStorageValue(), OnboardingStepState.Completed.ToString(), new
            {
                elapsedSeconds = (int)Math.Max(0, (progress.CompletedAtUtc.Value - progress.StartedAtUtc).TotalSeconds)
            }, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ApiResponse<OnboardingStatusDto>.Fail($"Onboarding completion failed: {ex.Message}", [ex.Message]);
        }
    }

    public async Task<ApiResponse<OnboardingStatusDto>> ResetAsync(OnboardingResetRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        if (hostEnvironment.IsProduction())
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Onboarding reset is blocked in production.");
        }

        if (!request.Confirm)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Reset confirmation is required.");
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        progress.CurrentStep = OnboardingStep.Welcome.ToStorageValue();
        progress.CompletedStepsCsv = string.Empty;
        progress.SkippedStepsCsv = string.Empty;
        progress.IsCompleted = false;
        progress.CompletedAtUtc = null;
        progress.CompletedByUserId = null;
        progress.FirstPropertyId = null;
        progress.SelectedPlanName = null;
        progress.StartedAtUtc = DateTimeOffset.UtcNow;
        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        progress.Version++;

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is not null)
        {
            company.OnboardingState = OnboardingStep.Welcome.ToStorageValue();
        }

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingReset", null, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.reset", OnboardingStep.Welcome.ToStorageValue(), OnboardingStepState.InProgress.ToString(), null, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(progress, cancellationToken));
    }

    private async Task<OnboardingStatusDto> BuildStatusAsync(OnboardingProgress progress, CancellationToken cancellationToken)
    {
        var completed = ParseSteps(progress.CompletedStepsCsv);
        var skipped = ParseSteps(progress.SkippedStepsCsv);
        var completedOrSkipped = new HashSet<OnboardingStep>(completed);
        completedOrSkipped.UnionWith(skipped);

        var blockers = await CalculateBlockersAsync(progress, completedOrSkipped, cancellationToken);
        var reviewSummary = await BuildReviewSummaryAsync(progress, completed, skipped, blockers, cancellationToken);
        var trustedPlanName = await ResolveTrustedPlanNameAsync(progress.CompanyId, cancellationToken);

        var currentStep = ResolveCurrentStep(progress, completedOrSkipped, blockers);
        var remainingSteps = WorkflowSteps
            .Where(step => !completedOrSkipped.Contains(step))
            .Select(step => step.ToStorageValue())
            .ToList();

        var checklist = await BuildChecklistAsync(progress, completedOrSkipped, skipped, blockers, cancellationToken);

        var denominator = WorkflowSteps.Length;
        var percent = denominator == 0
            ? 0
            : (int)Math.Round((completedOrSkipped.Count / (double)denominator) * 100, MidpointRounding.AwayFromZero);

        return new OnboardingStatusDto
        {
            CompanyId = progress.CompanyId,
            UserId = progress.UserId,
            CurrentStep = currentStep.ToStorageValue(),
            CurrentStepState = ResolveStepState(currentStep, completed, skipped, blockers).ToString(),
            CompletedSteps = completed.Select(item => item.ToStorageValue()).ToList(),
            RemainingSteps = remainingSteps,
            SkippedSteps = skipped.Select(item => item.ToStorageValue()).ToList(),
            Blockers = blockers,
            Checklist = checklist,
            ReviewSummary = reviewSummary,
            PercentComplete = Math.Clamp(percent, 0, 100),
            NextRecommendedAction = blockers.FirstOrDefault(item => item.Step == currentStep.ToStorageValue())?.Message
                ?? (currentStep == OnboardingStep.Completed ? "Open /get-started to continue." : $"Complete {currentStep.ToStorageValue()}"),
            SafeLinks = BuildSafeLinks(currentStep),
            StartedAtUtc = progress.StartedAtUtc,
            SelectedPlanName = progress.SelectedPlanName ?? trustedPlanName,
            FirstPropertyId = progress.FirstPropertyId,
            IsCompleted = progress.IsCompleted,
            CompletedAtUtc = progress.CompletedAtUtc,
            CompletedByUserId = progress.CompletedByUserId,
            LastUpdatedAtUtc = progress.LastUpdatedAtUtc,
            Version = progress.Version
        };
    }

    private async Task<OnboardingStatusDto> BuildNotStartedStatusAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
    {
        var emptyProgress = new OnboardingProgress
        {
            CompanyId = companyId,
            UserId = userId,
            CurrentStep = OnboardingStep.Welcome.ToStorageValue(),
            StartedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            Version = 0,
            IsCompleted = false
        };

        var completed = new HashSet<OnboardingStep>();
        var skipped = new HashSet<OnboardingStep>();
        var blockers = await CalculateBlockersAsync(emptyProgress, completed, cancellationToken);
        var reviewSummary = await BuildReviewSummaryAsync(emptyProgress, completed, skipped, blockers, cancellationToken);
        var checklist = await BuildChecklistAsync(emptyProgress, completed, skipped, blockers, cancellationToken);
        var trustedPlanName = await ResolveTrustedPlanNameAsync(companyId, cancellationToken);

        return new OnboardingStatusDto
        {
            CompanyId = companyId,
            UserId = userId,
            CurrentStep = OnboardingStep.Welcome.ToStorageValue(),
            CurrentStepState = OnboardingStepState.NotStarted.ToString(),
            CompletedSteps = [],
            RemainingSteps = WorkflowSteps.Select(step => step.ToStorageValue()).ToList(),
            SkippedSteps = [],
            Blockers = blockers,
            Checklist = checklist,
            ReviewSummary = reviewSummary,
            PercentComplete = 0,
            NextRecommendedAction = "Start onboarding.",
            SafeLinks = BuildSafeLinks(OnboardingStep.Welcome),
            StartedAtUtc = emptyProgress.StartedAtUtc,
            SelectedPlanName = trustedPlanName,
            FirstPropertyId = null,
            IsCompleted = false,
            CompletedAtUtc = null,
            CompletedByUserId = null,
            LastUpdatedAtUtc = emptyProgress.LastUpdatedAtUtc,
            Version = 0
        };
    }

    private async Task CleanupCreatedKnowledgeArticleAsync(Guid companyId, Guid articleId, CancellationToken cancellationToken)
    {
        var article = await dbContext.PropertyKnowledgeArticles
            .FirstOrDefaultAsync(item => item.Id == articleId && item.CompanyId == companyId, cancellationToken);
        if (article is not null)
        {
            dbContext.PropertyKnowledgeArticles.Remove(article);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task CleanupCreatedDemoDataAsync(
        IReadOnlyCollection<Guest> createdGuests,
        IReadOnlyCollection<Reservation> createdReservations,
        IReadOnlyCollection<Conversation> createdConversations,
        IReadOnlyCollection<ConversationMessage> createdMessages,
        IReadOnlyCollection<PropertyKnowledgeArticle> createdKnowledgeItems,
        CancellationToken cancellationToken)
    {
        if (createdMessages.Count > 0)
        {
            dbContext.ConversationMessages.RemoveRange(createdMessages);
        }

        if (createdConversations.Count > 0)
        {
            dbContext.Conversations.RemoveRange(createdConversations);
        }

        if (createdReservations.Count > 0)
        {
            dbContext.Reservations.RemoveRange(createdReservations);
        }

        if (createdGuests.Count > 0)
        {
            dbContext.Guests.RemoveRange(createdGuests);
        }

        if (createdKnowledgeItems.Count > 0)
        {
            dbContext.PropertyKnowledgeArticles.RemoveRange(createdKnowledgeItems);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyCollection<OnboardingSafeLinkDto> BuildSafeLinks(OnboardingStep currentStep)
    {
        var links = new List<OnboardingSafeLinkDto>
        {
            new() { Rel = "self", Href = "/onboarding" },
            new() { Rel = "current_step", Href = $"/onboarding/{ToRouteSegment(currentStep)}" },
            new() { Rel = "host_inbox", Href = "/host/conversations" },
            new() { Rel = "property_knowledge", Href = "/host/properties" },
            new() { Rel = "whatsapp_settings", Href = "/host/settings/whatsapp" },
            new() { Rel = "billing", Href = "/host/settings/billing" },
            new() { Rel = "team_settings", Href = "/host/settings/organization" }
        };

        return links;
    }

    private async Task<IReadOnlyCollection<OnboardingChecklistItemDto>> BuildChecklistAsync(
        OnboardingProgress progress,
        IReadOnlySet<OnboardingStep> completedOrSkipped,
        IReadOnlySet<OnboardingStep> skipped,
        IReadOnlyCollection<OnboardingBlockerDto> blockers,
        CancellationToken cancellationToken)
    {
        var items = new List<OnboardingChecklistItemDto>
        {
            new()
            {
                Key = "organization_profile_complete",
                Status = completedOrSkipped.Contains(OnboardingStep.OrganizationProfile) ? "complete" : "incomplete",
                Optional = false,
                Recommendation = "Confirm organization profile details."
            },
            new()
            {
                Key = "plan_confirmation_complete",
                Status = completedOrSkipped.Contains(OnboardingStep.PlanConfirmation) ? "complete" : "incomplete",
                Optional = false,
                Recommendation = "Confirm your selected plan in onboarding."
            },
            new()
            {
                Key = "first_property_complete",
                Status = completedOrSkipped.Contains(OnboardingStep.FirstProperty) ? "complete" : "incomplete",
                Optional = false,
                Recommendation = "Create or select your first property through onboarding."
            },
            new()
            {
                Key = "team_invited_or_skipped",
                Status = completedOrSkipped.Contains(OnboardingStep.TeamInvitations) || skipped.Contains(OnboardingStep.TeamInvitations)
                    ? "complete"
                    : "optional",
                Optional = true,
                Recommendation = "Invite teammates or skip for now."
            },
            new()
            {
                Key = "whatsapp_configured_or_skipped",
                Status = skipped.Contains(OnboardingStep.WhatsAppSetup) || completedOrSkipped.Contains(OnboardingStep.WhatsAppSetup)
                    ? "complete"
                    : "optional",
                Optional = true,
                Recommendation = "Configure WhatsApp only if your operating model requires it."
            },
            new()
            {
                Key = "ai_provider_ready",
                Status = completedOrSkipped.Contains(OnboardingStep.AiProviderSetup) ? "complete" : "incomplete",
                Optional = false,
                Recommendation = "Confirm deterministic fallback or configure OpenAI provider."
            },
            new()
            {
                Key = "knowledge_setup_complete",
                Status = completedOrSkipped.Contains(OnboardingStep.KnowledgeBaseSetup) ? "complete" : "incomplete",
                Optional = false,
                Recommendation = "Add your first knowledge item through onboarding."
            },
            new()
            {
                Key = "demo_data_complete_or_skipped",
                Status = completedOrSkipped.Contains(OnboardingStep.DemoData) || skipped.Contains(OnboardingStep.DemoData)
                    ? "complete"
                    : "optional",
                Optional = true,
                Recommendation = "Create demo data or skip this optional step."
            },
            new()
            {
                Key = "review_confirmed",
                Status = completedOrSkipped.Contains(OnboardingStep.Review) ? "complete" : "incomplete",
                Optional = false,
                Recommendation = "Review and confirm onboarding details before completion."
            },
            new()
            {
                Key = "readiness_checks_pass",
                Status = blockers.Count == 0 ? "complete" : "blocked",
                Optional = false,
                Recommendation = "Resolve blockers before completing onboarding."
            }
        };

        _ = progress;
        _ = cancellationToken;
        return items;
    }

    private async Task<OnboardingReviewSummaryDto> BuildReviewSummaryAsync(
        OnboardingProgress progress,
        IReadOnlySet<OnboardingStep> completed,
        IReadOnlySet<OnboardingStep> skipped,
        IReadOnlyCollection<OnboardingBlockerDto> blockers,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == progress.CompanyId, cancellationToken);

        Property? property = null;
        if (progress.FirstPropertyId.HasValue)
        {
            property = await dbContext.Properties
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CompanyId == progress.CompanyId && item.Id == progress.FirstPropertyId.Value && !item.IsDeleted, cancellationToken);
        }

        var invitations = await dbContext.OrganizationInvitations
            .AsNoTracking()
            .Where(item => item.CompanyId == progress.CompanyId)
            .Select(item => new
            {
                item.Email,
                item.Role,
                item.AcceptedAtUtc,
                item.RevokedAtUtc,
                item.ExpiresAtUtc,
                item.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var invitationSummaries = invitations
            .OrderByDescending(item => item.CreatedAt)
            .Take(10)
            .Select(item => new OnboardingReviewInvitationDto
            {
                Email = item.Email,
                Role = item.Role,
                Status = item.AcceptedAtUtc.HasValue
                    ? "Accepted"
                    : item.RevokedAtUtc.HasValue
                        ? "Revoked"
                        : item.ExpiresAtUtc <= DateTimeOffset.UtcNow
                            ? "Expired"
                            : "Pending"
            })
            .ToList();

        string? knowledgeTitle = null;
        if (progress.FirstPropertyId.HasValue)
        {
            var activeKnowledgeItems = await dbContext.PropertyKnowledgeArticles
                .AsNoTracking()
                .Where(item => item.CompanyId == progress.CompanyId
                    && item.PropertyId == progress.FirstPropertyId.Value
                    && !item.IsDeleted
                    && item.IsActive)
                .Select(item => new { item.Title, item.UpdatedAt })
                .ToListAsync(cancellationToken);

            knowledgeTitle = activeKnowledgeItems
                .OrderByDescending(item => item.UpdatedAt)
                .Select(item => item.Title)
                .FirstOrDefault();
        }

        var integrations = await dbContext.WhatsAppIntegrations
            .AsNoTracking()
            .Where(item => item.CompanyId == progress.CompanyId && item.IsActive)
            .Select(item => new { item.DisplayName, item.CreatedAt })
            .ToListAsync(cancellationToken);
        var integrationName = integrations
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.DisplayName)
            .FirstOrDefault();

        return new OnboardingReviewSummaryDto
        {
            OrganizationName = company?.Name,
            OrganizationSlug = company?.Slug,
            OrganizationSupportEmail = company?.Email,
            OrganizationTimeZone = company?.TimeZone,
            SelectedPlanName = progress.SelectedPlanName,
            FirstPropertyId = progress.FirstPropertyId,
            FirstPropertyName = property?.Name,
            TeamInvitationsState = ResolveStepState(OnboardingStep.TeamInvitations, completed, skipped, blockers).ToString(),
            TeamInvitations = invitationSummaries,
            WhatsAppSetupState = ResolveStepState(OnboardingStep.WhatsAppSetup, completed, skipped, blockers).ToString(),
            WhatsAppIntegrationName = integrationName,
            AiProviderState = ResolveStepState(OnboardingStep.AiProviderSetup, completed, skipped, blockers).ToString(),
            AiProvider = aiProviderOptions.Value.Provider,
            KnowledgeSetupState = ResolveStepState(OnboardingStep.KnowledgeBaseSetup, completed, skipped, blockers).ToString(),
            KnowledgeTitle = knowledgeTitle,
            DemoDataState = ResolveStepState(OnboardingStep.DemoData, completed, skipped, blockers).ToString()
        };
    }

    private async Task<IReadOnlyCollection<OnboardingBlockerDto>> CalculateBlockersAsync(
        OnboardingProgress progress,
        IReadOnlySet<OnboardingStep> completedOrSkipped,
        CancellationToken cancellationToken)
    {
        var blockers = new List<OnboardingBlockerDto>();

        if (!completedOrSkipped.Contains(OnboardingStep.OrganizationProfile))
        {
            return blockers;
        }

        var hasPlan = !string.IsNullOrWhiteSpace(progress.SelectedPlanName)
            || !string.IsNullOrWhiteSpace(await ResolveTrustedPlanNameAsync(progress.CompanyId, cancellationToken));
        if (!hasPlan)
        {
            blockers.Add(new OnboardingBlockerDto
            {
                Step = OnboardingStep.PlanConfirmation.ToStorageValue(),
                Code = "plan_missing",
                Message = "No trusted plan was found. Ensure default Free plan provisioning is configured."
            });
        }

        if (completedOrSkipped.Contains(OnboardingStep.FirstProperty) && progress.FirstPropertyId.HasValue)
        {
            var propertyExists = await dbContext.Properties.AsNoTracking().AnyAsync(item =>
                item.CompanyId == progress.CompanyId
                && item.Id == progress.FirstPropertyId.Value
                && !item.IsDeleted, cancellationToken);
            if (!propertyExists)
            {
                blockers.Add(new OnboardingBlockerDto
                {
                    Step = OnboardingStep.FirstProperty.ToStorageValue(),
                    Code = "property_missing",
                    Message = "The selected onboarding property no longer exists. Create a new property."
                });
            }
        }

        var provider = aiProviderOptions.Value.Provider;
        if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey) || string.IsNullOrWhiteSpace(openAiOptions.Value.Model)))
        {
            blockers.Add(new OnboardingBlockerDto
            {
                Step = OnboardingStep.AiProviderSetup.ToStorageValue(),
                Code = "ai_provider_not_ready",
                Message = "OpenAI configuration is incomplete."
            });
        }

        return blockers;
    }

    private static OnboardingStep ResolveCurrentStep(
        OnboardingProgress progress,
        IReadOnlySet<OnboardingStep> completedOrSkipped,
        IReadOnlyCollection<OnboardingBlockerDto> blockers)
    {
        if (progress.IsCompleted)
        {
            return OnboardingStep.Completed;
        }

        foreach (var step in WorkflowSteps)
        {
            if (completedOrSkipped.Contains(step))
            {
                continue;
            }

            var blocked = blockers.Any(item => string.Equals(item.Step, step.ToStorageValue(), StringComparison.OrdinalIgnoreCase));
            if (blocked)
            {
                return step;
            }

            var unmetPrerequisite = WorkflowSteps
                .Where(candidate => candidate.Rank() < step.Rank())
                .Any(candidate => !completedOrSkipped.Contains(candidate) && !OptionalSteps.Contains(candidate));
            if (unmetPrerequisite)
            {
                continue;
            }

            return step;
        }

        return OnboardingStep.Review;
    }

    private static OnboardingStepState ResolveStepState(
        OnboardingStep step,
        IReadOnlySet<OnboardingStep> completed,
        IReadOnlySet<OnboardingStep> skipped,
        IReadOnlyCollection<OnboardingBlockerDto> blockers)
    {
        if (completed.Contains(step))
        {
            return OnboardingStepState.Completed;
        }

        if (skipped.Contains(step))
        {
            return OnboardingStepState.Skipped;
        }

        if (blockers.Any(item => string.Equals(item.Step, step.ToStorageValue(), StringComparison.OrdinalIgnoreCase)))
        {
            return OnboardingStepState.Blocked;
        }

        return OnboardingStepState.InProgress;
    }

    private static IReadOnlySet<OnboardingStep> ParseSteps(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return new HashSet<OnboardingStep>();
        }

        var values = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var steps = new HashSet<OnboardingStep>();
        foreach (var value in values)
        {
            if (OnboardingStepExtensions.TryParse(value, out var step))
            {
                steps.Add(step);
            }
        }

        return steps;
    }

    private static string ToCsv(IEnumerable<OnboardingStep> steps)
    {
        return string.Join(',', steps.OrderBy(item => item.Rank()).Select(item => item.ToStorageValue()));
    }

    private static string ToRouteSegment(OnboardingStep step)
    {
        return step switch
        {
            OnboardingStep.Welcome => "welcome",
            OnboardingStep.OrganizationProfile => "organization",
            OnboardingStep.PlanConfirmation => "plan",
            OnboardingStep.FirstProperty => "property",
            OnboardingStep.TeamInvitations => "team",
            OnboardingStep.WhatsAppSetup => "whatsapp",
            OnboardingStep.AiProviderSetup => "ai",
            OnboardingStep.KnowledgeBaseSetup => "knowledge",
            OnboardingStep.DemoData => "demo",
            OnboardingStep.Review => "review",
            OnboardingStep.Completed => "complete",
            _ => "welcome"
        };
    }

    private void CompleteStep(OnboardingProgress progress, OnboardingStep step)
    {
        var completed = ParseSteps(progress.CompletedStepsCsv).ToHashSet();
        var skipped = ParseSteps(progress.SkippedStepsCsv).ToHashSet();
        completed.Add(step);
        skipped.Remove(step);

        progress.CompletedStepsCsv = ToCsv(completed);
        progress.SkippedStepsCsv = ToCsv(skipped);
        progress.CurrentStep = step.ToStorageValue();

        var resolvedCurrent = ResolveCurrentStep(progress, completed.Union(skipped).ToHashSet(), []);
        progress.CurrentStep = resolvedCurrent.ToStorageValue();
    }

    private void SkipStep(OnboardingProgress progress, OnboardingStep step)
    {
        var completed = ParseSteps(progress.CompletedStepsCsv).ToHashSet();
        var skipped = ParseSteps(progress.SkippedStepsCsv).ToHashSet();
        if (!OptionalSteps.Contains(step))
        {
            return;
        }

        skipped.Add(step);
        completed.Remove(step);

        progress.CompletedStepsCsv = ToCsv(completed);
        progress.SkippedStepsCsv = ToCsv(skipped);

        var resolvedCurrent = ResolveCurrentStep(progress, completed.Union(skipped).ToHashSet(), []);
        progress.CurrentStep = resolvedCurrent.ToStorageValue();
    }

    private bool CanActOnStep(OnboardingProgress progress, OnboardingStep target)
    {
        if (progress.IsCompleted)
        {
            return target == OnboardingStep.Completed;
        }

        var completed = ParseSteps(progress.CompletedStepsCsv);
        var skipped = ParseSteps(progress.SkippedStepsCsv);
        var completeSet = completed.Union(skipped).ToHashSet();

        var unmetRequiredPriorStep = WorkflowSteps
            .Where(step => step != OnboardingStep.Welcome && step.Rank() < target.Rank() && !OptionalSteps.Contains(step))
            .Any(step => !completeSet.Contains(step));

        return !unmetRequiredPriorStep;
    }

    private async Task<OnboardingProgress> GetOrCreateProgressAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
    {
        var existing = await FindProgressAsync(companyId, userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new OnboardingProgress
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            CurrentStep = OnboardingStep.Welcome.ToStorageValue(),
            IsCompleted = false,
            StartedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            Version = 1
        };

        await dbContext.OnboardingProgressRecords.AddAsync(created, cancellationToken);
        await AddAuditLogAsync(companyId, created.Id, "OnboardingStarted", null, cancellationToken);
        await AddOnboardingEventAsync(companyId, userId, "onboarding.started", created.CurrentStep, OnboardingStepState.InProgress.ToString(), null, cancellationToken);
        return created;
    }

    private Task<OnboardingProgress?> FindProgressAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.OnboardingProgressRecords
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.UserId == userId, cancellationToken);
    }

    private Task<OnboardingProgress?> FindCompletedCompanyProgressAsync(Guid companyId, CancellationToken cancellationToken)
    {
        return dbContext.OnboardingProgressRecords
            .Where(item => item.CompanyId == companyId && item.IsCompleted)
            .OrderByDescending(item => item.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task AddAuditLogAsync(Guid companyId, Guid entityId, string action, object? metadata, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(OnboardingProgress),
            EntityId = entityId,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                companyId,
                userId = tenantContext.UserId,
                metadata
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private async Task AddOnboardingEventAsync(
        Guid companyId,
        Guid userId,
        string eventName,
        string step,
        string state,
        object? metadata,
        CancellationToken cancellationToken)
    {
        await dbContext.OnboardingEvents.AddAsync(new OnboardingEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            EventName = eventName,
            Step = step,
            State = state,
            MetadataJson = metadata is null ? "{}" : JsonSerializer.Serialize(metadata)
        }, cancellationToken);
    }

    private bool TryGetContext(out Guid companyId, out Guid userId, out string error)
    {
        companyId = tenantContext.CompanyId ?? Guid.Empty;
        userId = tenantContext.UserId ?? Guid.Empty;

        if (!tenantContext.IsAuthenticated || companyId == Guid.Empty || userId == Guid.Empty)
        {
            error = "Authenticated tenant context is required.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private async Task<string?> ResolveTrustedPlanNameAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var snapshot = await subscriptionEntitlementService.GetCurrentSnapshotAsync(companyId, cancellationToken);
        return NormalizeOptional(snapshot.PlanDisplayName) ?? NormalizeOptional(snapshot.PlanName);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Slugify(string input)
    {
        var chars = input.Trim().ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) ? character : '-').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("--", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("--", "-", StringComparison.Ordinal);
        }

        return collapsed.Trim('-');
    }
}
