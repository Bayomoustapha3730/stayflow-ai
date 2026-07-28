using StayFlow.Api.Common;
using StayFlow.Api.DTOs.PropertyKnowledge;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class PropertyKnowledgeServiceTests
{
    [Fact]
    public async Task GetAsync_WithExistingAccessiblePropertyAndZeroKnowledge_ReturnsEmptyPagedResult()
    {
        var repository = new FakePropertyKnowledgeRepository();
        var property = NewProperty(repository.CompanyId);
        repository.Properties.Add(property);

        var service = new PropertyKnowledgeService(
            repository,
            new FakeCurrentTenantContext(repository.CompanyId, userId: Guid.NewGuid()));

        var response = await service.GetAsync(property.Id, new PropertyKnowledgeListQuery(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(0, response.Data.TotalCount);
        Assert.Empty(response.Data.Items);
        Assert.Equal(1, response.Data.PageNumber);
    }

    [Fact]
    public async Task CreateAsync_WithAccessibleProperty_CreatesFirstKnowledgeItemWithTenantScopedFields()
    {
        var repository = new FakePropertyKnowledgeRepository();
        var property = NewProperty(repository.CompanyId);
        repository.Properties.Add(property);
        var userId = Guid.NewGuid();

        var service = new PropertyKnowledgeService(
            repository,
            new FakeCurrentTenantContext(repository.CompanyId, userId: userId));

        var response = await service.CreateAsync(property.Id, new CreatePropertyKnowledgeRequest
        {
            Category = PropertyKnowledgeCategory.WiFi,
            Title = "Guest Wi-Fi",
            Summary = "Wi-Fi details",
            Content = "SSID: StayFlowGuest. Password: DemoStay2026.",
            Tags = ["wifi", "internet"],
            Priority = 5,
            IsActive = true
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);

        var created = Assert.Single(repository.Articles);
        Assert.Equal(repository.CompanyId, created.CompanyId);
        Assert.Equal(property.Id, created.PropertyId);
        Assert.Equal(userId, created.CreatedByUserId);
        Assert.Equal(userId, created.UpdatedByUserId);
        Assert.False(created.IsApproved);
        Assert.True(created.IsActive);
    }

    [Fact]
    public async Task GetAsync_WithCrossTenantProperty_ReturnsNotFound()
    {
        var repository = new FakePropertyKnowledgeRepository();
        var otherCompanyId = Guid.NewGuid();
        var crossTenantProperty = NewProperty(otherCompanyId);
        repository.Properties.Add(crossTenantProperty);

        var service = new PropertyKnowledgeService(
            repository,
            new FakeCurrentTenantContext(repository.CompanyId, userId: Guid.NewGuid()));

        var response = await service.GetAsync(crossTenantProperty.Id, new PropertyKnowledgeListQuery(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
    }

    [Fact]
    public async Task GetAsync_WithNonexistentProperty_ReturnsNotFound()
    {
        var repository = new FakePropertyKnowledgeRepository();
        var service = new PropertyKnowledgeService(
            repository,
            new FakeCurrentTenantContext(repository.CompanyId, userId: Guid.NewGuid()));

        var response = await service.GetAsync(Guid.NewGuid(), new PropertyKnowledgeListQuery(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
    }

    [Fact]
    public async Task CreateAsync_WithCrossTenantProperty_ReturnsNotFound()
    {
        var repository = new FakePropertyKnowledgeRepository();
        var otherCompanyId = Guid.NewGuid();
        var crossTenantProperty = NewProperty(otherCompanyId);
        repository.Properties.Add(crossTenantProperty);

        var service = new PropertyKnowledgeService(
            repository,
            new FakeCurrentTenantContext(repository.CompanyId, userId: Guid.NewGuid()));

        var response = await service.CreateAsync(crossTenantProperty.Id, new CreatePropertyKnowledgeRequest
        {
            Category = PropertyKnowledgeCategory.WiFi,
            Title = "Guest Wi-Fi",
            Content = "SSID: StayFlowGuest. Password: DemoStay2026.",
            Tags = ["wifi"],
            Priority = 2,
            IsActive = false
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
        Assert.Empty(repository.Articles);
    }

    [Fact]
    public async Task CreateAsync_WithoutAuthenticatedUserId_ReturnsFailure()
    {
        var repository = new FakePropertyKnowledgeRepository();
        var property = NewProperty(repository.CompanyId);
        repository.Properties.Add(property);

        var service = new PropertyKnowledgeService(
            repository,
            new FakeCurrentTenantContext(repository.CompanyId, userId: null, isAuthenticated: true));

        var response = await service.CreateAsync(property.Id, new CreatePropertyKnowledgeRequest
        {
            Category = PropertyKnowledgeCategory.WiFi,
            Title = "Guest Wi-Fi",
            Content = "SSID: StayFlowGuest. Password: DemoStay2026.",
            Tags = ["wifi"],
            Priority = 2,
            IsActive = true
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated user context is missing or invalid.", response.Message);
    }

    private static Property NewProperty(Guid companyId)
    {
        return new Property
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Demo Nairobi Apartment",
            AddressLine1 = "Westlands",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            Description = "Demo property",
            IsActive = true,
            IsDeleted = false
        };
    }

    private sealed class FakePropertyKnowledgeRepository : IPropertyKnowledgeRepository
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public List<Property> Properties { get; } = [];
        public List<PropertyKnowledgeArticle> Articles { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<Property?> GetPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
        {
            var property = Properties.FirstOrDefault(p =>
                p.CompanyId == companyId
                && p.Id == propertyId
                && p.IsActive
                && !p.IsDeleted);

            return Task.FromResult(property);
        }

        public Task<PagedResult<PropertyKnowledgeArticle>> GetPagedAsync(Guid companyId, Guid propertyId, PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
        {
            var filtered = Articles
                .Where(article => article.CompanyId == companyId && article.PropertyId == propertyId && !article.IsDeleted)
                .ToList();

            var pageNumber = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;

            var items = filtered
                .OrderByDescending(article => article.Priority)
                .ThenByDescending(article => article.UpdatedAt)
                .ThenBy(article => article.Title)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult(new PagedResult<PropertyKnowledgeArticle>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = filtered.Count
            });
        }

        public Task<PropertyKnowledgeArticle?> GetByIdAsync(Guid companyId, Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
        {
            var item = Articles.FirstOrDefault(article =>
                article.CompanyId == companyId
                && article.PropertyId == propertyId
                && article.Id == knowledgeId
                && !article.IsDeleted);

            return Task.FromResult(item);
        }

        public Task<IReadOnlyCollection<PropertyKnowledgeArticle>> GetApprovedActiveForPropertyAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<PropertyKnowledgeArticle> items = Articles
                .Where(article => article.CompanyId == companyId
                    && article.PropertyId == propertyId
                    && article.IsApproved
                    && article.IsActive
                    && !article.IsDeleted)
                .ToList();

            return Task.FromResult(items);
        }

        public Task AddAsync(PropertyKnowledgeArticle article, CancellationToken cancellationToken)
        {
            Articles.Add(article);
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

    private sealed class FakeCurrentTenantContext(Guid? companyId, Guid? userId = null, bool isAuthenticated = true) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId { get; } = "test-correlation";
        public bool IsAuthenticated { get; } = isAuthenticated;
    }
}
