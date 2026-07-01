using System.Text.Json;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;

namespace StayFlow.Api.Services;

public sealed class PropertyService(IPropertyRepository propertyRepository) : IPropertyService
{
    public async Task<ApiResponse<PagedResult<PropertySummaryDto>>> GetAsync(PropertyQueryParameters query, CancellationToken cancellationToken)
    {
        var properties = await propertyRepository.GetAsync(query, cancellationToken);

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
        var property = await propertyRepository.GetByIdAsync(id, cancellationToken);
        return property is null
            ? ApiResponse<PropertyDto>.Fail("Property was not found.")
            : ApiResponse<PropertyDto>.Ok(MapToDto(property));
    }

    public async Task<ApiResponse<PropertyDto>> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken)
    {
        var validation = PropertyRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<PropertyDto>.Fail("Property validation failed.", validation.Errors);
        }

        if (!await propertyRepository.CompanyExistsAsync(request.CompanyId, cancellationToken))
        {
            return ApiResponse<PropertyDto>.Fail("Company was not found.");
        }

        var property = new Property
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            Name = request.Name.Trim(),
            AddressLine1 = request.AddressLine1.Trim(),
            AddressLine2 = NormalizeOptional(request.AddressLine2),
            City = request.City.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            TimeZone = request.TimeZone.Trim(),
            Description = NormalizeOptional(request.Description),
            IsActive = true
        };

        ReplaceChildren(property, request.Amenities, request.HouseRules, request.LocalRecommendations, request.EmergencyContacts, request.KnowledgeBaseItems);

        await propertyRepository.AddAsync(property, cancellationToken);
        await AddAuditLogAsync("Created", property, cancellationToken);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyDto>.Ok(MapToDto(property), "Property created successfully.");
    }

    public async Task<ApiResponse<PropertyDto>> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken)
    {
        var validation = PropertyRequestValidator.Validate(request);
        if (!validation.IsValid)
        {
            return ApiResponse<PropertyDto>.Fail("Property validation failed.", validation.Errors);
        }

        var property = await propertyRepository.GetByIdAsync(id, cancellationToken);
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

        ReplaceChildren(property, request.Amenities, request.HouseRules, request.LocalRecommendations, request.EmergencyContacts, request.KnowledgeBaseItems);

        await AddAuditLogAsync("Updated", property, cancellationToken);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<PropertyDto>.Ok(MapToDto(property), "Property updated successfully.");
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var property = await propertyRepository.GetByIdAsync(id, cancellationToken);
        if (property is null)
        {
            return ApiResponse<object>.Fail("Property was not found.");
        }

        property.IsActive = false;
        foreach (var item in property.Amenities.Cast<AuditableEntity>()
                     .Concat(property.HouseRules)
                     .Concat(property.LocalRecommendations)
                     .Concat(property.EmergencyContacts)
                     .Concat(property.KnowledgeBaseItems))
        {
            if (item is Amenity amenity) amenity.IsActive = false;
            if (item is HouseRule rule) rule.IsActive = false;
            if (item is LocalRecommendation recommendation) recommendation.IsActive = false;
            if (item is EmergencyContact contact) contact.IsActive = false;
            if (item is KnowledgeBaseItem knowledgeBaseItem) knowledgeBaseItem.IsActive = false;
        }

        await AddAuditLogAsync("Deleted", property, cancellationToken);
        await propertyRepository.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { property.Id }, "Property deleted successfully.");
    }

    private static void ReplaceChildren(
        Property property,
        IReadOnlyCollection<AmenityRequest> amenities,
        IReadOnlyCollection<HouseRuleRequest> houseRules,
        IReadOnlyCollection<LocalRecommendationRequest> recommendations,
        IReadOnlyCollection<EmergencyContactRequest> contacts,
        IReadOnlyCollection<PropertyKnowledgeBaseItemRequest> knowledgeBaseItems)
    {
        foreach (var amenity in property.Amenities)
        {
            amenity.IsActive = false;
        }

        foreach (var rule in property.HouseRules)
        {
            rule.IsActive = false;
        }

        foreach (var recommendation in property.LocalRecommendations)
        {
            recommendation.IsActive = false;
        }

        foreach (var contact in property.EmergencyContacts)
        {
            contact.IsActive = false;
        }

        foreach (var item in property.KnowledgeBaseItems)
        {
            item.IsActive = false;
        }

        foreach (var amenity in amenities)
        {
            property.Amenities.Add(new Amenity { Id = Guid.NewGuid(), Name = amenity.Name.Trim(), Description = NormalizeOptional(amenity.Description), IsActive = true });
        }

        foreach (var rule in houseRules)
        {
            property.HouseRules.Add(new HouseRule { Id = Guid.NewGuid(), Title = rule.Title.Trim(), Description = rule.Description.Trim(), IsActive = true });
        }

        foreach (var recommendation in recommendations)
        {
            property.LocalRecommendations.Add(new LocalRecommendation
            {
                Id = Guid.NewGuid(),
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
            property.EmergencyContacts.Add(new EmergencyContact { Id = Guid.NewGuid(), Name = contact.Name.Trim(), Role = contact.Role.Trim(), PhoneNumber = contact.PhoneNumber.Trim(), IsActive = true });
        }

        foreach (var item in knowledgeBaseItems)
        {
            property.KnowledgeBaseItems.Add(new KnowledgeBaseItem
            {
                Id = Guid.NewGuid(),
                CompanyId = property.CompanyId,
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
            Details = JsonSerializer.Serialize(new { property.CompanyId, property.Name, property.IsActive }),
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
            Amenities = property.Amenities.Where(item => item.IsActive).Select(item => new AmenityDto { Id = item.Id, Name = item.Name, Description = item.Description }).ToList(),
            HouseRules = property.HouseRules.Where(item => item.IsActive).Select(item => new HouseRuleDto { Id = item.Id, Title = item.Title, Description = item.Description }).ToList(),
            LocalRecommendations = property.LocalRecommendations.Where(item => item.IsActive).Select(item => new LocalRecommendationDto { Id = item.Id, Name = item.Name, Category = item.Category, Description = item.Description, Address = item.Address, PhoneNumber = item.PhoneNumber }).ToList(),
            EmergencyContacts = property.EmergencyContacts.Where(item => item.IsActive).Select(item => new EmergencyContactDto { Id = item.Id, Name = item.Name, Role = item.Role, PhoneNumber = item.PhoneNumber }).ToList(),
            KnowledgeBaseItems = property.KnowledgeBaseItems.Where(item => item.IsActive).Select(item => new PropertyKnowledgeBaseItemDto { Id = item.Id, Title = item.Title, Content = item.Content }).ToList()
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
