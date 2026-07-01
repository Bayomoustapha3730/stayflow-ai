using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Companies;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class CompanyServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidRequest_CreatesCompanyAndAuditLog()
    {
        var repository = new FakeCompanyRepository();
        var service = new CompanyService(repository);

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Acme Hosts", response.Data.Name);
        Assert.Single(repository.Companies);
        Assert.Single(repository.AuditLogs);
        Assert.Equal("Created", repository.AuditLogs[0].Action);
        Assert.Equal(repository.Companies[0].Id, repository.AuditLogs[0].EntityId);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeCompanyRepository();
        var service = new CompanyService(repository);

        var response = await service.CreateAsync(new CreateCompanyRequest
        {
            Name = "",
            Email = "not-an-email",
            PhoneNumber = "0700000000",
            CountryCode = "KEN",
            TimeZone = ""
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.NotEmpty(response.Errors);
        Assert.Empty(repository.Companies);
        Assert.Empty(repository.AuditLogs);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsPagedNameSearchResults()
    {
        var repository = new FakeCompanyRepository();
        repository.Companies.Add(NewCompany("Coast Stay Hosts"));
        repository.Companies.Add(NewCompany("Nairobi Homes"));
        repository.Companies.Add(NewCompany("Coast Villas"));
        var service = new CompanyService(repository);

        var response = await service.GetAsync(new CompanyQueryParameters
        {
            Search = "coast",
            PageNumber = 1,
            PageSize = 1
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.TotalCount);
        var company = Assert.Single(response.Data.Items);
        Assert.Contains("Coast", company.Name);
    }

    [Fact]
    public async Task UpdateAsync_WithExistingCompany_UpdatesCompanyAndAuditLog()
    {
        var repository = new FakeCompanyRepository();
        var company = NewCompany("Old Name");
        repository.Companies.Add(company);
        var service = new CompanyService(repository);

        var response = await service.UpdateAsync(company.Id, new UpdateCompanyRequest
        {
            Name = "New Name",
            Email = "new@example.com",
            PhoneNumber = "+254711111111",
            CountryCode = "ke",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("New Name", company.Name);
        Assert.Equal("KE", company.CountryCode);
        Assert.Single(repository.AuditLogs);
        Assert.Equal("Updated", repository.AuditLogs[0].Action);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingCompany_SoftDeletesCompanyAndAuditLog()
    {
        var repository = new FakeCompanyRepository();
        var company = NewCompany("Delete Me");
        repository.Companies.Add(company);
        var service = new CompanyService(repository);

        var response = await service.DeleteAsync(company.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.False(company.IsActive);
        Assert.Single(repository.AuditLogs);
        Assert.Equal("Deleted", repository.AuditLogs[0].Action);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task GetByIdAsync_WithSoftDeletedCompany_ReturnsNotFound()
    {
        var repository = new FakeCompanyRepository();
        var company = NewCompany("Inactive");
        company.IsActive = false;
        repository.Companies.Add(company);
        var service = new CompanyService(repository);

        var response = await service.GetByIdAsync(company.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Null(response.Data);
    }

    private static CreateCompanyRequest ValidCreateRequest()
    {
        return new CreateCompanyRequest
        {
            Name = "Acme Hosts",
            LegalName = "Acme Hosts Ltd",
            Email = "ops@acme.example",
            PhoneNumber = "+254700000001",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        };
    }

    private static Company NewCompany(string name)
    {
        return new Company
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = $"{name.Replace(" ", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}@example.com",
            PhoneNumber = $"+2547{Random.Shared.Next(10000000, 99999999)}",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        public List<Company> Companies { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public int SaveChangesCallCount { get; private set; }

        public Task<PagedResult<Company>> GetAsync(
            CompanyQueryParameters query,
            CancellationToken cancellationToken)
        {
            var pageNumber = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;
            var companies = Companies
                .Where(company => company.IsActive)
                .Where(company => string.IsNullOrWhiteSpace(query.Search)
                    || company.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                .OrderBy(company => company.Name)
                .ToList();

            return Task.FromResult(new PagedResult<Company>
            {
                Items = companies
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = companies.Count
            });
        }

        public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Companies.FirstOrDefault(company => company.Id == id && company.IsActive));
        }

        public Task AddAsync(Company company, CancellationToken cancellationToken)
        {
            Companies.Add(company);
            return Task.CompletedTask;
        }

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
