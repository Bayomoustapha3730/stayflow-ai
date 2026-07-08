using System.Text.Json;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class PropertyServiceTests
{
    [Fact]
    public void PropertyRequestDtos_DoNotExposeCompanyIdTenantSelector()
    {
        Assert.Null(typeof(CreatePropertyRequest).GetProperty("CompanyId"));
        Assert.Null(typeof(UpdatePropertyRequest).GetProperty("CompanyId"));
        Assert.Null(typeof(PropertyQueryParameters).GetProperty("CompanyId"));
    }

    [Fact]
    public async Task CreateAsync_WithAuthenticatedTenant_CreatesPropertyAndAuditLog()
    {
        var repository = new FakePropertyRepository();
        var tenantContext = new FakeCurrentTenantContext(repository.CompanyId);
        var service = new PropertyService(repository, tenantContext);

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Property created successfully.", response.Message);
        Assert.Single(repository.Properties);
        Assert.Equal(repository.CompanyId, repository.Properties[0].CompanyId);
        Assert.Single(repository.Properties[0].PropertyAmenities);
        Assert.Single(repository.Properties[0].PropertyHouseRules);
        Assert.Single(repository.Properties[0].PropertyRecommendations);
        Assert.Single(repository.Properties[0].PropertyEmergencyContacts);
        Assert.Single(repository.Properties[0].PropertyKnowledgeArticles);
        Assert.All(repository.Properties[0].PropertyAmenities, amenity => Assert.Equal(repository.Properties[0].Id, amenity.PropertyId));
        Assert.All(repository.Properties[0].PropertyHouseRules, rule => Assert.Equal(repository.Properties[0].Id, rule.PropertyId));
        Assert.All(repository.Properties[0].PropertyRecommendations, recommendation => Assert.Equal(repository.Properties[0].Id, recommendation.PropertyId));
        Assert.All(repository.Properties[0].PropertyEmergencyContacts, contact => Assert.Equal(repository.Properties[0].Id, contact.PropertyId));
        Assert.All(repository.Properties[0].PropertyKnowledgeArticles, article =>
        {
            Assert.Equal(repository.Properties[0].Id, article.PropertyId);
            Assert.Equal(repository.CompanyId, article.CompanyId);
        });
        Assert.Single(repository.AuditLogs);
        Assert.Equal("Created", repository.AuditLogs[0].Action);
    }

    [Fact]
    public async Task CreateAsync_UsesAuthenticatedTenantCompanyId()
    {
        var repository = new FakePropertyRepository();
        var tenantCompanyId = repository.CompanyId;
        var service = new PropertyService(repository, new FakeCurrentTenantContext(tenantCompanyId));

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(tenantCompanyId, Assert.Single(repository.Properties).CompanyId);
    }

    [Fact]
    public async Task CreateAsync_WithMissingCompany_ReturnsFailure()
    {
        var repository = new FakePropertyRepository { CompanyExists = false };
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Company was not found.", response.Message);
        Assert.Empty(repository.Properties);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakePropertyRepository();
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.CreateAsync(new CreatePropertyRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Property name is required.", response.Errors);
        Assert.NotEmpty(response.Errors);
    }

    [Fact]
    public async Task CreateAsync_WithMissingTenantContext_ReturnsFailure()
    {
        var repository = new FakePropertyRepository();
        var service = new PropertyService(repository, new FakeCurrentTenantContext(null, isAuthenticated: true));

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is missing or invalid.", response.Message);
        Assert.Empty(repository.Properties);
    }

    [Fact]
    public async Task GetAsync_SearchesAndPaginatesTenantPropertiesIncludingInactiveOnes()
    {
        var repository = new FakePropertyRepository();
        repository.Properties.Add(NewProperty(repository.CompanyId, "Coast Villa"));
        repository.Properties.Add(NewProperty(repository.CompanyId, "Nairobi Loft"));
        repository.Properties.Add(NewProperty(repository.CompanyId, "Coast Studio", isActive: false));
        repository.Properties.Add(NewProperty(Guid.NewGuid(), "Coast Other Tenant"));
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetAsync(new PropertyQueryParameters
        {
            Search = "coast",
            PageNumber = 1,
            PageSize = 1
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.TotalCount);
        Assert.Single(response.Data.Items);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsInactivePropertyWhenNotDeleted()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(repository.CompanyId, "Inactive Property", isActive: false);
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetByIdAsync(property.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.False(response.Data.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_ExcludesSoftDeletedProperties()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(repository.CompanyId, "Deleted Property", isDeleted: true);
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetByIdAsync(property.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
    }

    [Fact]
    public async Task GetByIdAsync_ExcludesInactiveNestedData()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(repository.CompanyId, "Nested Property");
        property.PropertyAmenities.Add(new PropertyAmenity { Id = Guid.NewGuid(), PropertyId = property.Id, Name = "WiFi", IsActive = true });
        property.PropertyAmenities.Add(new PropertyAmenity { Id = Guid.NewGuid(), PropertyId = property.Id, Name = "Inactive Pool", IsActive = false });
        property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle { Id = Guid.NewGuid(), CompanyId = repository.CompanyId, PropertyId = property.Id, Title = "Active FAQ", Content = "Answer", IsActive = true });
        property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle { Id = Guid.NewGuid(), CompanyId = repository.CompanyId, PropertyId = property.Id, Title = "Inactive FAQ", Content = "Old answer", IsActive = false });
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetByIdAsync(property.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        var amenity = Assert.Single(response.Data.PropertyAmenities);
        Assert.Equal("WiFi", amenity.Name);
        var article = Assert.Single(response.Data.PropertyKnowledgeArticles);
        Assert.Equal("Active FAQ", article.Title);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesNestedActiveData()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(repository.CompanyId, "Old Name");
        property.PropertyAmenities.Add(new PropertyAmenity { Id = Guid.NewGuid(), PropertyId = property.Id, Name = "WiFi", IsActive = true });
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.UpdateAsync(property.Id, new UpdatePropertyRequest
        {
            Name = "Updated Name",
            AddressLine1 = "Updated address",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            PropertyAmenities = [new PropertyAmenityRequest { Name = "Pool" }]
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Updated Name", property.Name);
        Assert.Equal(2, property.PropertyAmenities.Count);
        var activeAmenity = Assert.Single(property.PropertyAmenities, propertyAmenity => propertyAmenity.IsActive);
        Assert.Equal("Pool", activeAmenity.Name);
        Assert.Contains(property.PropertyAmenities, propertyAmenity => propertyAmenity.Name == "WiFi" && !propertyAmenity.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_TenantCannotUpdateOtherTenantProperty()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(Guid.NewGuid(), "Other Tenant Property");
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.UpdateAsync(property.Id, new UpdatePropertyRequest
        {
            Name = "Updated Name",
            AddressLine1 = "Updated address",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
    }

    [Fact]
    public async Task GetByIdAsync_TenantCannotRetrieveOtherTenantProperty()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(Guid.NewGuid(), "Other Tenant Property");
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetByIdAsync(property.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
    }

    [Fact]
    public async Task DeleteAsync_TenantCannotDeleteOtherTenantProperty()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(Guid.NewGuid(), "Other Tenant Property");
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.DeleteAsync(property.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
        Assert.False(property.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesPropertyWithoutChangingOperationalStatus()
    {
        var repository = new FakePropertyRepository();
        var userId = Guid.NewGuid();
        var property = NewProperty(repository.CompanyId, "Delete Me", isActive: false);
        property.PropertyAmenities.Add(new PropertyAmenity { Id = Guid.NewGuid(), PropertyId = property.Id, Name = "WiFi", IsActive = true });
        property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle { Id = Guid.NewGuid(), CompanyId = repository.CompanyId, PropertyId = property.Id, Title = "FAQ", Content = "Answer", IsActive = true });
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId, userId: userId));

        var response = await service.DeleteAsync(property.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(property.IsActive);
        Assert.True(property.IsDeleted);
        Assert.NotNull(property.DeletedAt);
        Assert.Equal(userId, property.DeletedBy);
        Assert.All(property.PropertyAmenities, propertyAmenity => Assert.True(propertyAmenity.IsActive));
        Assert.All(property.PropertyKnowledgeArticles, item => Assert.True(item.IsActive));
    }

    [Fact]
    public async Task DeleteAsync_AddsDeletionAuditLogWithTenantUserAndCorrelation()
    {
        var repository = new FakePropertyRepository();
        var userId = Guid.NewGuid();
        var property = NewProperty(repository.CompanyId, "Delete Me");
        repository.Properties.Add(property);
        var service = new PropertyService(repository, new FakeCurrentTenantContext(repository.CompanyId, userId: userId, correlationId: "test-correlation"));

        var response = await service.DeleteAsync(property.Id, CancellationToken.None);

        Assert.True(response.Success);
        var auditLog = Assert.Single(repository.AuditLogs);
        Assert.Equal(nameof(Property), auditLog.EntityName);
        Assert.Equal(property.Id, auditLog.EntityId);
        Assert.Equal("Deleted", auditLog.Action);
        Assert.NotEqual(default, auditLog.CreatedAt);
        using var details = JsonDocument.Parse(auditLog.Details!);
        Assert.Equal(repository.CompanyId, details.RootElement.GetProperty("CompanyId").GetGuid());
        Assert.True(details.RootElement.GetProperty("IsDeleted").GetBoolean());
        Assert.Equal(userId, details.RootElement.GetProperty("AuthenticatedUserId").GetGuid());
        Assert.Equal("test-correlation", details.RootElement.GetProperty("CorrelationId").GetString());
    }

    private static CreatePropertyRequest ValidCreateRequest()
    {
        return new CreatePropertyRequest
        {
            Name = "Demo Property",
            AddressLine1 = "Westlands",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            PropertyAmenities = [new PropertyAmenityRequest { Name = "WiFi" }],
            PropertyHouseRules = [new PropertyHouseRuleRequest { Title = "No smoking", Description = "Smoking is not allowed." }],
            PropertyRecommendations = [new PropertyRecommendationRequest { Name = "Java House", Category = "Restaurant" }],
            PropertyEmergencyContacts = [new PropertyEmergencyContactRequest { Name = "Security Desk", Role = "Security", PhoneNumber = "+254700000003" }],
            PropertyKnowledgeArticles = [new PropertyKnowledgeArticleRequest { Title = "Check-in", Content = "Check-in is after 2 PM." }]
        };
    }

    private static Property NewProperty(Guid companyId, string name, bool isActive = true, bool isDeleted = false)
    {
        return new Property
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = name,
            AddressLine1 = "Address",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeCurrentTenantContext(
        Guid? companyId,
        bool isAuthenticated = true,
        Guid? userId = null,
        string? correlationId = null) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public string? CorrelationId { get; } = correlationId ?? "test-correlation";
        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class FakePropertyRepository : IPropertyRepository
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public bool CompanyExists { get; init; } = true;
        public List<Property> Properties { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<PagedResult<Property>> GetAsync(Guid companyId, PropertyQueryParameters query, CancellationToken cancellationToken)
        {
            var pageNumber = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;
            var properties = Properties
                .Where(property => property.CompanyId == companyId)
                .Where(property => !property.IsDeleted)
                .Where(property => string.IsNullOrWhiteSpace(query.Search) || property.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(property => property.Name)
                .ToList();

            return Task.FromResult(new PagedResult<Property>
            {
                Items = properties.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = properties.Count
            });
        }

        public Task<Property?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Properties.FirstOrDefault(property => property.Id == id && property.CompanyId == companyId && !property.IsDeleted));
        }

        public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CompanyExists && companyId == CompanyId);
        }

        public Task AddAsync(Property property, CancellationToken cancellationToken)
        {
            Properties.Add(property);
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
