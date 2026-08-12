using System.Text.Json;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.PropertyKnowledge;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class PropertyKnowledgeService(
    IPropertyKnowledgeRepository propertyKnowledgeRepository,
    ICurrentTenantContext currentTenantContext) : IPropertyKnowledgeService
{
    private const int MaxPriority = 10;
    private const int MaxTitleLength = 200;
    private const int MaxSummaryLength = 280;
    private const int MaxContentLength = 6000;
    private const int MaxTags = 12;
    private const int MaxTagLength = 40;

    public async Task<ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>> GetAsync(Guid propertyId, PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>.Fail(tenantError, [tenantError]);
        }

        var property = await propertyKnowledgeRepository.GetPropertyAsync(companyId, propertyId, cancellationToken);
        if (property is null)
        {
            return ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>.Fail("Property was not found.");
        }

        var paged = await propertyKnowledgeRepository.GetPagedAsync(companyId, propertyId, query, cancellationToken);
        return ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>.Ok(new PagedResult<PropertyKnowledgeSummaryResponse>
        {
            Items = paged.Items.Select(item => MapSummary(item, property.Name)).ToList(),
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        });
    }

    public async Task<ApiResponse<PropertyKnowledgeDetailResponse>> GetByIdAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail(tenantError, [tenantError]);
        }

        var item = await propertyKnowledgeRepository.GetByIdAsync(companyId, propertyId, knowledgeId, cancellationToken);
        return item is null
            ? ApiResponse<PropertyKnowledgeDetailResponse>.Fail("Knowledge item was not found.")
            : ApiResponse<PropertyKnowledgeDetailResponse>.Ok(MapDetail(item));
    }

    public async Task<ApiResponse<PropertyKnowledgeDetailResponse>> CreateAsync(Guid propertyId, CreatePropertyKnowledgeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail(tenantError, [tenantError]);
        }

        if (!TryGetUserId(out var userId, out var userError))
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail(userError, [userError]);
        }

        var property = await propertyKnowledgeRepository.GetPropertyAsync(companyId, propertyId, cancellationToken);
        if (property is null)
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail("Property was not found.");
        }

        var validationErrors = Validate(request.Category, request.Title, request.Summary, request.Content, request.Tags, request.Priority);
        if (validationErrors.Count > 0)
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail("Knowledge validation failed.", validationErrors);
        }

        var article = new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            Category = request.Category,
            Title = request.Title.Trim(),
            Summary = NormalizeOptional(request.Summary),
            Content = request.Content.Trim(),
            Tags = SerializeTags(request.Tags),
            Priority = request.Priority,
            IsApproved = false,
            IsActive = request.IsActive,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        };

        await propertyKnowledgeRepository.AddAsync(article, cancellationToken);
        await AddAuditLogAsync("Created", article, cancellationToken);
        await propertyKnowledgeRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyKnowledgeDetailResponse>.Ok(
            MapDetail(article, property.Name),
            "Knowledge item created successfully.");
    }

    public async Task<ApiResponse<PropertyKnowledgeDetailResponse>> UpdateAsync(Guid propertyId, Guid knowledgeId, UpdatePropertyKnowledgeRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail(tenantError, [tenantError]);
        }

        var validationErrors = Validate(request.Category, request.Title, request.Summary, request.Content, request.Tags, request.Priority);
        if (validationErrors.Count > 0)
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail("Knowledge validation failed.", validationErrors);
        }

        var item = await propertyKnowledgeRepository.GetByIdAsync(companyId, propertyId, knowledgeId, cancellationToken);
        if (item is null)
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail("Knowledge item was not found.");
        }

        item.Category = request.Category;
        item.Title = request.Title.Trim();
        item.Summary = NormalizeOptional(request.Summary);
        item.Content = request.Content.Trim();
        item.Tags = SerializeTags(request.Tags);
        item.Priority = request.Priority;
        item.IsActive = request.IsActive;
        item.UpdatedByUserId = currentTenantContext.UserId;

        if (item.IsApproved)
        {
            item.IsApproved = false;
            item.ApprovedAt = null;
            item.ApprovedByUserId = null;
        }

        await AddAuditLogAsync("Updated", item, cancellationToken);
        await propertyKnowledgeRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyKnowledgeDetailResponse>.Ok(MapDetail(item), "Knowledge item updated successfully.");
    }

    public Task<ApiResponse<PropertyKnowledgeDetailResponse>> ApproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
    {
        return ChangeApprovalAsync(propertyId, knowledgeId, true, cancellationToken);
    }

    public Task<ApiResponse<PropertyKnowledgeDetailResponse>> UnapproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
    {
        return ChangeApprovalAsync(propertyId, knowledgeId, false, cancellationToken);
    }

    public Task<ApiResponse<PropertyKnowledgeDetailResponse>> ActivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
    {
        return ChangeActiveStateAsync(propertyId, knowledgeId, true, cancellationToken);
    }

    public Task<ApiResponse<PropertyKnowledgeDetailResponse>> DeactivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
    {
        return ChangeActiveStateAsync(propertyId, knowledgeId, false, cancellationToken);
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<object>.Fail(tenantError, [tenantError]);
        }

        var item = await propertyKnowledgeRepository.GetByIdAsync(companyId, propertyId, knowledgeId, cancellationToken);
        if (item is null)
        {
            return ApiResponse<object>.Fail("Knowledge item was not found.");
        }

        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        item.DeletedByUserId = currentTenantContext.UserId;
        item.IsActive = false;
        item.IsApproved = false;
        item.ApprovedAt = null;
        item.ApprovedByUserId = null;
        item.UpdatedByUserId = currentTenantContext.UserId;

        await AddAuditLogAsync("Deleted", item, cancellationToken);
        await propertyKnowledgeRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { item.Id }, "Knowledge item deleted successfully.");
    }

    private async Task<ApiResponse<PropertyKnowledgeDetailResponse>> ChangeApprovalAsync(Guid propertyId, Guid knowledgeId, bool approved, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail(tenantError, [tenantError]);
        }

        var item = await propertyKnowledgeRepository.GetByIdAsync(companyId, propertyId, knowledgeId, cancellationToken);
        if (item is null)
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail("Knowledge item was not found.");
        }

        item.IsApproved = approved;
        item.ApprovedAt = approved ? DateTimeOffset.UtcNow : null;
        item.ApprovedByUserId = approved ? currentTenantContext.UserId : null;
        item.UpdatedByUserId = currentTenantContext.UserId;

        await AddAuditLogAsync(approved ? "Approved" : "Unapproved", item, cancellationToken);
        await propertyKnowledgeRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyKnowledgeDetailResponse>.Ok(MapDetail(item), approved ? "Knowledge item approved successfully." : "Knowledge item unapproved successfully.");
    }

    private async Task<ApiResponse<PropertyKnowledgeDetailResponse>> ChangeActiveStateAsync(Guid propertyId, Guid knowledgeId, bool isActive, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail(tenantError, [tenantError]);
        }

        var item = await propertyKnowledgeRepository.GetByIdAsync(companyId, propertyId, knowledgeId, cancellationToken);
        if (item is null)
        {
            return ApiResponse<PropertyKnowledgeDetailResponse>.Fail("Knowledge item was not found.");
        }

        item.IsActive = isActive;
        item.UpdatedByUserId = currentTenantContext.UserId;

        await AddAuditLogAsync(isActive ? "Activated" : "Deactivated", item, cancellationToken);
        await propertyKnowledgeRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyKnowledgeDetailResponse>.Ok(MapDetail(item), isActive ? "Knowledge item activated successfully." : "Knowledge item deactivated successfully.");
    }

    private bool TryGetCompanyId(out Guid companyId, out string error)
    {
        if (!currentTenantContext.IsAuthenticated)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is required.";
            return false;
        }

        if (currentTenantContext.CompanyId is not { } tenantCompanyId || tenantCompanyId == Guid.Empty)
        {
            companyId = Guid.Empty;
            error = "Authenticated tenant context is missing or invalid.";
            return false;
        }

        companyId = tenantCompanyId;
        error = string.Empty;
        return true;
    }

    private bool TryGetUserId(out Guid userId, out string error)
    {
        if (!currentTenantContext.IsAuthenticated)
        {
            userId = Guid.Empty;
            error = "Authenticated user context is required.";
            return false;
        }

        if (currentTenantContext.UserId is not { } currentUserId || currentUserId == Guid.Empty)
        {
            userId = Guid.Empty;
            error = "Authenticated user context is missing or invalid.";
            return false;
        }

        userId = currentUserId;
        error = string.Empty;
        return true;
    }

    private async Task AddAuditLogAsync(string action, PropertyKnowledgeArticle article, CancellationToken cancellationToken)
    {
        await propertyKnowledgeRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(PropertyKnowledgeArticle),
            EntityId = article.Id,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                article.CompanyId,
                article.PropertyId,
                article.Category,
                article.Title,
                article.IsApproved,
                article.IsActive,
                article.IsDeleted,
                AuthenticatedUserId = currentTenantContext.UserId,
                currentTenantContext.CorrelationId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static IReadOnlyCollection<string> Validate(PropertyKnowledgeCategory category, string? title, string? summary, string? content, IReadOnlyCollection<string> tags, int priority)
    {
        var errors = new List<string>();

        if (!Enum.IsDefined(category))
        {
            errors.Add("Knowledge category is required.");
        }

        AddRequired(errors, title, "Knowledge title", MaxTitleLength);
        if (!string.IsNullOrWhiteSpace(summary) && summary.Trim().Length > MaxSummaryLength)
        {
            errors.Add($"Knowledge summary must be {MaxSummaryLength} characters or fewer.");
        }

        AddRequired(errors, content, "Knowledge content", MaxContentLength);

        if (priority < 0 || priority > MaxPriority)
        {
            errors.Add($"Knowledge priority must be between 0 and {MaxPriority}.");
        }

        var normalizedTags = NormalizeTags(tags);
        if (normalizedTags.Count > MaxTags)
        {
            errors.Add($"Knowledge tags must contain at most {MaxTags} values.");
        }

        if (normalizedTags.Any(tag => tag.Length > MaxTagLength))
        {
            errors.Add($"Each knowledge tag must be {MaxTagLength} characters or fewer.");
        }

        return errors;
    }

    private static void AddRequired(ICollection<string> errors, string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            errors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    private static PropertyKnowledgeSummaryResponse MapSummary(PropertyKnowledgeArticle article, string propertyName)
    {
        return new PropertyKnowledgeSummaryResponse
        {
            Id = article.Id,
            PropertyId = article.PropertyId,
            PropertyName = propertyName,
            Category = article.Category,
            CategoryLabel = CategoryLabel(article.Category),
            Title = article.Title,
            Summary = article.Summary,
            Tags = DeserializeTags(article.Tags),
            Priority = article.Priority,
            IsApproved = article.IsApproved,
            IsActive = article.IsActive,
            ApprovedAt = article.ApprovedAt,
            ApprovedBy = article.ApprovedByUser?.FullName,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            CanBeUsedByAI = CanBeUsedByAI(article)
        };
    }

    private static PropertyKnowledgeDetailResponse MapDetail(PropertyKnowledgeArticle article, string? propertyName = null)
    {
        return new PropertyKnowledgeDetailResponse
        {
            Id = article.Id,
            PropertyId = article.PropertyId,
            PropertyName = propertyName ?? article.Property?.Name ?? string.Empty,
            Category = article.Category,
            CategoryLabel = CategoryLabel(article.Category),
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content,
            Tags = DeserializeTags(article.Tags),
            Priority = article.Priority,
            IsApproved = article.IsApproved,
            IsActive = article.IsActive,
            ApprovedAt = article.ApprovedAt,
            ApprovedBy = article.ApprovedByUser?.FullName,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            EstimatedCharacterContribution = article.Content.Length + article.Title.Length + (article.Summary?.Length ?? 0),
            CanBeUsedByAI = CanBeUsedByAI(article)
        };
    }

    private static bool CanBeUsedByAI(PropertyKnowledgeArticle article)
    {
        return article.IsApproved && article.IsActive && !article.IsDeleted;
    }

    private static string SerializeTags(IReadOnlyCollection<string> tags)
    {
        return string.Join(',', NormalizeTags(tags));
    }

    private static IReadOnlyCollection<string> DeserializeTags(string? tags)
    {
        return NormalizeTags((tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static List<string> NormalizeTags(IEnumerable<string> tags)
    {
        return tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => string.Join('-', tag.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string CategoryLabel(PropertyKnowledgeCategory category)
    {
        return category switch
        {
            PropertyKnowledgeCategory.WiFi => "Wi-Fi",
            PropertyKnowledgeCategory.CheckIn => "Check-in",
            PropertyKnowledgeCategory.HouseRules => "House rules",
            PropertyKnowledgeCategory.LocalRecommendations => "Local recommendations",
            _ => category.ToString()
        };
    }
}