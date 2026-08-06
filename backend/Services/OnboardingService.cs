using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Onboarding;
using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public sealed class OnboardingService(
    ApplicationDbContext dbContext,
    ICurrentTenantContext tenantContext) : IOnboardingService
{
    public async Task<ApiResponse<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await FindProgressAsync(companyId, userId, cancellationToken);
        if (progress is null)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Onboarding has not been initialized.");
        }

        return ApiResponse<OnboardingStatusDto>.Ok(Map(progress));
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
                CurrentStep = OnboardingStep.OrganizationCreated.ToStorageValue(),
                IsCompleted = false,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            };

            await dbContext.OnboardingProgressRecords.AddAsync(progress, cancellationToken);
            await AddAuditLogAsync(companyId, progress.Id, "OnboardingStarted", cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApiResponse<OnboardingStatusDto>.Ok(Map(progress));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteOrganizationStepAsync(CompleteOnboardingOrganizationStepRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Organization name is required.");
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
        company.OnboardingState = OnboardingStep.OrganizationCreated.ToStorageValue();

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanAdvance(progress.CurrentStep, OnboardingStep.OrganizationCreated))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Onboarding step order is invalid.");
        }

        SetStep(progress, OnboardingStep.OrganizationCreated);

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingOrganizationCompleted", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(Map(progress));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompletePlanStepAsync(CompleteOnboardingPlanStepRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        if (string.IsNullOrWhiteSpace(request.PlanName))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Plan name is required.");
        }

        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IsActive
                && (string.Equals(item.Name, request.PlanName.Trim(), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.DisplayName, request.PlanName.Trim(), StringComparison.OrdinalIgnoreCase)), cancellationToken);
        if (plan is null)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Selected plan was not found.");
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanAdvance(progress.CurrentStep, OnboardingStep.PlanSelected))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Onboarding step order is invalid.");
        }

        progress.SelectedPlanName = plan.Name;
        SetStep(progress, OnboardingStep.PlanSelected);

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is not null)
        {
            company.OnboardingState = OnboardingStep.PlanSelected.ToStorageValue();
        }

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingPlanCompleted", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(Map(progress));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompletePropertyStepAsync(CompleteOnboardingPropertyStepRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var propertyExists = await dbContext.Properties
            .AsNoTracking()
            .AnyAsync(item => item.Id == request.PropertyId && item.CompanyId == companyId && !item.IsDeleted, cancellationToken);
        if (!propertyExists)
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Property was not found.");
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanAdvance(progress.CurrentStep, OnboardingStep.FirstPropertyCreated))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Onboarding step order is invalid.");
        }

        progress.FirstPropertyId = request.PropertyId;
        SetStep(progress, OnboardingStep.FirstPropertyCreated);

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is not null)
        {
            company.OnboardingState = OnboardingStep.FirstPropertyCreated.ToStorageValue();
        }

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingPropertyCompleted", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(Map(progress));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteTeamStepAsync(CompleteOnboardingTeamStepRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (!CanAdvance(progress.CurrentStep, OnboardingStep.TeammatesInvited))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Onboarding step order is invalid.");
        }

        SetStep(progress, OnboardingStep.TeammatesInvited);

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is not null)
        {
            company.OnboardingState = OnboardingStep.TeammatesInvited.ToStorageValue();
        }

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingTeamCompleted", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(Map(progress));
    }

    public async Task<ApiResponse<OnboardingStatusDto>> CompleteOnboardingAsync(CancellationToken cancellationToken)
    {
        if (!TryGetContext(out var companyId, out var userId, out var error))
        {
            return ApiResponse<OnboardingStatusDto>.Fail(error);
        }

        var progress = await GetOrCreateProgressAsync(companyId, userId, cancellationToken);
        if (StepRank(progress.CurrentStep) < StepRank(OnboardingStep.TeammatesInvited.ToStorageValue()))
        {
            return ApiResponse<OnboardingStatusDto>.Fail("Onboarding cannot be completed before all steps are done.");
        }

        progress.IsCompleted = true;
        progress.CompletedAtUtc = DateTimeOffset.UtcNow;
        SetStep(progress, OnboardingStep.Completed);

        var company = await dbContext.Companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is not null)
        {
            company.OnboardingState = OnboardingStep.Completed.ToStorageValue();
        }

        await AddAuditLogAsync(companyId, progress.Id, "OnboardingCompleted", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<OnboardingStatusDto>.Ok(Map(progress));
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
            CurrentStep = OnboardingStep.OrganizationCreated.ToStorageValue(),
            IsCompleted = false,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await dbContext.OnboardingProgressRecords.AddAsync(created, cancellationToken);
        return created;
    }

    private Task<OnboardingProgress?> FindProgressAsync(Guid companyId, Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.OnboardingProgressRecords
            .FirstOrDefaultAsync(item => item.CompanyId == companyId && item.UserId == userId, cancellationToken);
    }

    private static void SetStep(OnboardingProgress progress, OnboardingStep step)
    {
        progress.CurrentStep = step.ToStorageValue();
        progress.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static bool CanAdvance(string currentStep, OnboardingStep target)
    {
        return StepRank(currentStep) <= StepRank(target.ToStorageValue());
    }

    private static int StepRank(string step)
    {
        return Enum.TryParse<OnboardingStep>(step, true, out var parsed)
            ? (int)parsed
            : 0;
    }

    private async Task AddAuditLogAsync(Guid companyId, Guid entityId, string action, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(OnboardingProgress),
            EntityId = entityId,
            Action = action,
            Details = $"{{\"companyId\":\"{companyId}\",\"userId\":\"{tenantContext.UserId}\"}}",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static OnboardingStatusDto Map(OnboardingProgress progress)
    {
        return new OnboardingStatusDto
        {
            CompanyId = progress.CompanyId,
            UserId = progress.UserId,
            CurrentStep = progress.CurrentStep,
            SelectedPlanName = progress.SelectedPlanName,
            FirstPropertyId = progress.FirstPropertyId,
            IsCompleted = progress.IsCompleted,
            CompletedAtUtc = progress.CompletedAtUtc,
            LastUpdatedAtUtc = progress.LastUpdatedAtUtc
        };
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