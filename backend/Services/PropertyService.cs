using System.Text.Json;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class PropertyService(IPropertyRepository propertyRepository, ICurrentTenantContext currentTenantContext) : IPropertyService
{
    public async Task<ApiResponse<PagedResult<PropertySummaryDto>>> GetAsync(PropertyQueryParameters query, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PagedResult<PropertySummaryDto>>.Fail(tenantError, [tenantError]);
        }

        var properties = await propertyRepository.GetAsync(companyId, query, cancellationToken);

        return ApiResponse<PagedResult<PropertySummaryDto>>.Ok(new PagedResult<PropertySummaryDto>
        {
            Items = properties.Items.Select(MapToSummaryDto).ToList(),
            PageNumber = properties.PageNumber,
            PageSize = properties.PageSize,
            TotalCount = properties.TotalCount
        });
    }

    public async Task<ApiResponse<PropertyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyDto>.Fail(tenantError, [tenantError]);
        }

        var property = await propertyRepository.GetByIdAsync(id, companyId, cancellationToken);
        return property is null
            ? ApiResponse<PropertyDto>.Fail("Property was not found.")
            : ApiResponse<PropertyDto>.Ok(MapToDto(property));
    }

    public async Task<ApiResponse<PropertyDto>> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyDto>.Fail(tenantError, [tenantError]);
        }

        var validation = PropertyRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<PropertyDto>.Fail("Property validation failed.", validation.Errors);
        }

        if (!await propertyRepository.CompanyExistsAsync(companyId, cancellationToken))
        {
            return ApiResponse<PropertyDto>.Fail("Company was not found.");
        }

        var property = new Property
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = NormalizeOptional(request.AddressLine2),
            City = request.City.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            TimeZone = request.TimeZone.Trim(),
            Description = NormalizeOptional(request.Description),
            IsActive = true
        };

        ReplaceChildren(property, request.PropertyAmenities, request.PropertyHouseRules, request.PropertyRecommendations, request.PropertyEmergencyContacts, request.PropertyKnowledgeArticles);

        await propertyRepository.AddAsync(property, cancellationToken);
        await AddAuditLogAsync("Created", property, cancellationToken);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyDto>.Ok(MapToDto(property), "Property created successfully.");
    }

    public async Task<ApiResponse<PropertyDto>> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<PropertyDto>.Fail(tenantError, [tenantError]);
        }

        var validation = PropertyRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<PropertyDto>.Fail("Property validation failed.", validation.Errors);
        }

        var property = await propertyRepository.GetByIdAsync(id, companyId, cancellationToken);
        if (property is null)
        {
            return ApiResponse<PropertyDto>.Fail("Property was not found.");
        }

        property.Name = request.Name.Trim();
        property.AddressLine1 = request.AddressLine1.Trim();
        property.AddressLine2 = NormalizeOptional(request.AddressLine2);
        property.City = request.City.Trim();
        property.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        property.TimeZone = request.TimeZone.Trim();
        property.Description = NormalizeOptional(request.Description);
        property.IsActive = request.IsActive;

        ReplaceChildren(property, request.PropertyAmenities, request.PropertyHouseRules, request.PropertyRecommendations, request.PropertyEmergencyContacts, request.PropertyKnowledgeArticles);

        await AddAuditLogAsync("Updated", property, cancellationToken);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyDto>.Ok(MapToDto(property), "Property updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(out var companyId, out var tenantError))
        {
            return ApiResponse<object>.Fail(tenantError, [tenantError]);
        }

        var property = await propertyRepository.GetByIdAsync(id, companyId, cancellationToken);
        if (property is null)
        {
            return ApiResponse<object>.Fail("Property was not found.");
        }

        property.IsDeleted = true;
        property.DeletedAt = DateTimeOffset.UtcNow;
        property.DeletedBy = currentTenantContext.UserId;

        await AddAuditLogAsync("Deleted", property, cancellationToken);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { property.Id }, "Property deleted successfully.");
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

    private static void ReplaceChildren(
        Property property,
        IReadOnlyCollection<PropertyAmenityRequest> amenities,
        IReadOnlyCollection<PropertyHouseRuleRequest> houseRules,
        IReadOnlyCollection<PropertyRecommendationRequest> recommendations,
        IReadOnlyCollection<PropertyEmergencyContactRequest> contacts,
        IReadOnlyCollection<PropertyKnowledgeArticleRequest> knowledgeArticles)
    {
        foreach (var amenity in property.PropertyAmenities)
        {
            amenity.IsActive = false;
        }

        foreach (var rule in property.PropertyHouseRules)
        {
            rule.IsActive = false;
        }

        foreach (var recommendation in property.PropertyRecommendations)
        {
            recommendation.IsActive = false;
        }

        foreach (var contact in property.PropertyEmergencyContacts)
        {
            contact.IsActive = false;
        }

        foreach (var item in property.PropertyKnowledgeArticles)
        {
            item.IsActive = false;
        }

        foreach (var amenity in amenities)
        {
            property.PropertyAmenities.Add(new PropertyAmenity
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                Name = amenity.Name.Trim(),
                Description = NormalizeOptional(amenity.Description),
                IsActive = true
            });
        }

        foreach (var rule in houseRules)
        {
            property.PropertyHouseRules.Add(new PropertyHouseRule
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                Title = rule.Title.Trim(),
                Description = rule.Description.Trim(),
                IsActive = true
            });
        }

        foreach (var recommendation in recommendations)
        {
            property.PropertyRecommendations.Add(new PropertyRecommendation
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                Name = recommendation.Name.Trim(),
                Category = recommendation.Category.Trim(),
                Description = NormalizeOptional(recommendation.Description),
                Address = NormalizeOptional(recommendation.Address),
                PhoneNumber = NormalizeOptional(recommendation.PhoneNumber),
                IsActive = true
            });
        }

        foreach (var contact in contacts)
        {
            property.PropertyEmergencyContacts.Add(new PropertyEmergencyContact
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                Name = contact.Name.Trim(),
                Role = contact.Role.Trim(),
                PhoneNumber = contact.PhoneNumber.Trim(),
                IsActive = true
            });
        }

        foreach (var item in knowledgeArticles)
        {
            property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
            {
                Id = Guid.NewGuid(),
                CompanyId = property.CompanyId,
                PropertyId = property.Id,
                Title = item.Title.Trim(),
                Content = item.Content.Trim(),
                IsActive = true
            });
        }
    }

    private async Task AddAuditLogAsync(string action, Property property, CancellationToken cancellationToken)
    {
        await propertyRepository.AddAuditLogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = nameof(Property),
            EntityId = property.Id,
            Action = action,
            Details = JsonSerializer.Serialize(new
            {
                property.CompanyId,
                property.Name,
                property.IsActive,
                property.IsDeleted,
                property.DeletedAt,
                property.DeletedBy,
                AuthenticatedUserId = currentTenantContext.UserId,
                currentTenantContext.CorrelationId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static PropertySummaryDto MapToSummaryDto(Property property)
    {
        return new PropertySummaryDto
        {
            Id = property.Id,
            CompanyId = property.CompanyId,
            Name = property.Name,
            City = property.City,
            CountryCode = property.CountryCode,
            IsActive = property.IsActive,
            CreatedAt = property.CreatedAt
        };
    }

    private static PropertyDto MapToDto(Property property)
    {
        return new PropertyDto
        {
            Id = property.Id,
            CompanyId = property.CompanyId,
            Name = property.Name,
            AddressLine1 = property.AddressLine1,
            AddressLine2 = property.AddressLine2,
            City = property.City,
            CountryCode = property.CountryCode,
            TimeZone = property.TimeZone,
            Description = property.Description,
            IsActive = property.IsActive,
            CreatedAt = property.CreatedAt,
            UpdatedAt = property.UpdatedAt,
            PropertyAmenities = property.PropertyAmenities.Where(item => item.IsActive).Select(item => new PropertyAmenityDto { Id = item.Id, Name = item.Name, Description = item.Description }).ToList(),
            PropertyHouseRules = property.PropertyHouseRules.Where(item => item.IsActive).Select(item => new PropertyHouseRuleDto { Id = item.Id, Title = item.Title, Description = item.Description }).ToList(),
            PropertyRecommendations = property.PropertyRecommendations.Where(item => item.IsActive).Select(item => new PropertyRecommendationDto { Id = item.Id, Name = item.Name, Category = item.Category, Description = item.Description, Address = item.Address, PhoneNumber = item.PhoneNumber }).ToList(),
            PropertyEmergencyContacts = property.PropertyEmergencyContacts.Where(item => item.IsActive).Select(item => new PropertyEmergencyContactDto { Id = item.Id, Name = item.Name, Role = item.Role, PhoneNumber = item.PhoneNumber }).ToList(),
            PropertyKnowledgeArticles = property.PropertyKnowledgeArticles.Where(item => item.IsActive).Select(item => new PropertyKnowledgeArticleDto { Id = item.Id, Title = item.Title, Content = item.Content }).ToList()
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
