using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class PropertyServiceTests
{
    [Fact]
    public async Task CreateAsync_WithNestedData_CreatesPropertyAndAuditLog()
    {
        var repository = new FakePropertyRepository();
        var service = new PropertyService(repository);

        var response = await service.CreateAsync(ValidCreateRequest(repository.CompanyId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Property created successfully.", response.Message);
        Assert.Single(repository.Properties);
        Assert.Single(repository.Properties[0].Amenities);
        Assert.Single(repository.Properties[0].HouseRules);
        Assert.Single(repository.Properties[0].LocalRecommendations);
        Assert.Single(repository.Properties[0].EmergencyContacts);
        Assert.Single(repository.Properties[0].KnowledgeBaseItems);
        Assert.Single(repository.AuditLogs);
        Assert.Equal("Created", repository.AuditLogs[0].Action);
    }

    [Fact]
    public async Task CreateAsync_WithMissingCompany_ReturnsFailure()
    {
        var repository = new FakePropertyRepository { CompanyExists = false };
        var service = new PropertyService(repository);

        var response = await service.CreateAsync(ValidCreateRequest(Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Company was not found.", response.Message);
        Assert.Empty(repository.Properties);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakePropertyRepository();
        var service = new PropertyService(repository);

        var response = await service.CreateAsync(new CreatePropertyRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("CompanyId is required.", response.Errors);
        Assert.NotEmpty(response.Errors);
    }

    [Fact]
    public async Task GetAsync_SearchesAndPaginatesActiveProperties()
    {
        var repository = new FakePropertyRepository();
        repository.Properties.Add(NewProperty(repository.CompanyId, "Coast Villa"));
        repository.Properties.Add(NewProperty(repository.CompanyId, "Nairobi Loft"));
        repository.Properties.Add(NewProperty(repository.CompanyId, "Coast Studio"));
        var service = new PropertyService(repository);

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
    public async Task UpdateAsync_ReplacesNestedActiveData()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(repository.CompanyId, "Old Name");
        property.Amenities.Add(new Amenity { Id = Guid.NewGuid(), Name = "WiFi", IsActive = true });
        repository.Properties.Add(property);
        var service = new PropertyService(repository);

        var response = await service.UpdateAsync(property.Id, new UpdatePropertyRequest
        {
            Name = "Updated Name",
            AddressLine1 = "Updated address",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            Amenities = [new AmenityRequest { Name = "Pool" }]
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Updated Name", property.Name);
        Assert.Equal(2, property.Amenities.Count);
        var activeAmenity = Assert.Single(property.Amenities, amenity => amenity.IsActive);
        Assert.Equal("Pool", activeAmenity.Name);
        Assert.Contains(property.Amenities, amenity => amenity.Name == "WiFi" && !amenity.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesPropertyAndNestedData()
    {
        var repository = new FakePropertyRepository();
        var property = NewProperty(repository.CompanyId, "Delete Me");
        property.Amenities.Add(new Amenity { Id = Guid.NewGuid(), Name = "WiFi", IsActive = true });
        property.KnowledgeBaseItems.Add(new KnowledgeBaseItem { Id = Guid.NewGuid(), CompanyId = repository.CompanyId, Title = "FAQ", Content = "Answer", IsActive = true });
        repository.Properties.Add(property);
        var service = new PropertyService(repository);

        var response = await service.DeleteAsync(property.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(property.IsActive);
        Assert.All(property.Amenities, amenity => Assert.False(amenity.IsActive));
        Assert.All(property.KnowledgeBaseItems, item => Assert.False(item.IsActive));
        Assert.Single(repository.AuditLogs);
        Assert.Equal("Deleted", repository.AuditLogs[0].Action);
    }

    private static CreatePropertyRequest ValidCreateRequest(Guid companyId)
    {
        return new CreatePropertyRequest
        {
            CompanyId = companyId,
            Name = "Demo Property",
            AddressLine1 = "Westlands",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            Amenities = [new AmenityRequest { Name = "WiFi" }],
            HouseRules = [new HouseRuleRequest { Title = "No smoking", Description = "Smoking is not allowed." }],
            LocalRecommendations = [new LocalRecommendationRequest { Name = "Java House", Category = "Restaurant" }],
            EmergencyContacts = [new EmergencyContactRequest { Name = "Security Desk", Role = "Security", PhoneNumber = "+254700000003" }],
            KnowledgeBaseItems = [new PropertyKnowledgeBaseItemRequest { Title = "Check-in", Content = "Check-in is after 2 PM." }]
        };
    }

    private static Property NewProperty(Guid companyId, string name)
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
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakePropertyRepository : IPropertyRepository
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public bool CompanyExists { get; init; } = true;
        public List<Property> Properties { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<PagedResult<Property>> GetAsync(PropertyQueryParameters query, CancellationToken cancellationToken)
        {
            var pageNumber = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;
            var properties = Properties
                .Where(property => property.IsActive)
                .Where(property => query.CompanyId is null || property.CompanyId == query.CompanyId)
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

        public Task<Property?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Properties.FirstOrDefault(property => property.Id == id && property.IsActive));
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
