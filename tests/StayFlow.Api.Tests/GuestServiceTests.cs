using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Guests;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class GuestServiceTests
{
    [Fact]
    public void GuestRequestDtos_DoNotExposeCompanyIdTenantSelector()
    {
        Assert.Null(typeof(CreateGuestRequest).GetProperty("CompanyId"));
        Assert.Null(typeof(UpdateGuestRequest).GetProperty("CompanyId"));
        Assert.Null(typeof(GuestQueryParameters).GetProperty("CompanyId"));
    }

    [Fact]
    public async Task CreateAsync_WithAuthenticatedTenant_CreatesGuest()
    {
        var repository = new FakeGuestRepository();
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Guest created successfully.", response.Message);
        var guest = Assert.Single(repository.Guests);
        Assert.Equal(repository.CompanyId, guest.CompanyId);
        Assert.Equal("Amina", guest.FirstName);
        Assert.Equal("Mwangi", guest.LastName);
        Assert.Equal("amina@example.com", guest.Email);
        Assert.Equal("+254700000111", guest.PhoneNumber);
        Assert.Equal("sw-ke", guest.PreferredLanguage);
        Assert.Equal("KE", guest.CountryCode);
        Assert.Single(repository.AuditLogs);
    }

    [Fact]
    public async Task UpdateAsync_WithAuthenticatedTenant_UpdatesGuest()
    {
        var repository = new FakeGuestRepository();
        var guest = NewGuest(repository.CompanyId, "Amina", "Mwangi");
        repository.Guests.Add(guest);
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.UpdateAsync(guest.Id, new UpdateGuestRequest
        {
            FirstName = "Grace",
            LastName = "Achieng",
            Email = "Grace@Example.COM",
            PhoneNumber = "+254700000222",
            PreferredLanguage = "EN",
            CountryCode = "ke",
            Notes = "Returning guest",
            IsActive = false
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Grace", guest.FirstName);
        Assert.Equal("Achieng", guest.LastName);
        Assert.Equal("grace@example.com", guest.Email);
        Assert.Equal("en", guest.PreferredLanguage);
        Assert.Equal("KE", guest.CountryCode);
        Assert.False(guest.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_WithTenantGuest_ReturnsGuest()
    {
        var repository = new FakeGuestRepository();
        var guest = NewGuest(repository.CompanyId, "Amina", "Mwangi");
        repository.Guests.Add(guest);
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetByIdAsync(guest.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(guest.Id, response.Data.Id);
    }

    [Fact]
    public async Task GetAsync_PaginatesTenantGuests()
    {
        var repository = new FakeGuestRepository();
        repository.Guests.Add(NewGuest(repository.CompanyId, "Amina", "Mwangi"));
        repository.Guests.Add(NewGuest(repository.CompanyId, "Brian", "Otieno"));
        repository.Guests.Add(NewGuest(repository.CompanyId, "Carol", "Wanjiku"));
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetAsync(new GuestQueryParameters { PageNumber = 1, PageSize = 2 }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(3, response.Data.TotalCount);
        Assert.Equal(2, response.Data.Items.Count);
    }

    [Fact]
    public async Task GetAsync_SearchesByName()
    {
        var repository = new FakeGuestRepository();
        repository.Guests.Add(NewGuest(repository.CompanyId, "Amina", "Mwangi"));
        repository.Guests.Add(NewGuest(repository.CompanyId, "Brian", "Otieno"));
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetAsync(new GuestQueryParameters { Search = "amina" }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Amina", Assert.Single(response.Data!.Items).FirstName);
    }

    [Fact]
    public async Task GetAsync_SearchesByEmail()
    {
        var repository = new FakeGuestRepository();
        repository.Guests.Add(NewGuest(repository.CompanyId, "Amina", "Mwangi", email: "amina@example.com"));
        repository.Guests.Add(NewGuest(repository.CompanyId, "Brian", "Otieno", email: "brian@example.com"));
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetAsync(new GuestQueryParameters { Search = "brian@example.com" }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Brian", Assert.Single(response.Data!.Items).FirstName);
    }

    [Fact]
    public async Task GetAsync_SearchesByPhone()
    {
        var repository = new FakeGuestRepository();
        repository.Guests.Add(NewGuest(repository.CompanyId, "Amina", "Mwangi", phoneNumber: "+254700000111"));
        repository.Guests.Add(NewGuest(repository.CompanyId, "Brian", "Otieno", phoneNumber: "+254700000222"));
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetAsync(new GuestQueryParameters { Search = "0222" }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Brian", Assert.Single(response.Data!.Items).FirstName);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeGuestRepository();
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.CreateAsync(new CreateGuestRequest
        {
            Email = "invalid",
            PhoneNumber = "0700000111",
            PreferredLanguage = "!",
            CountryCode = "KEN"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Guest validation failed.", response.Message);
        Assert.Contains("First name is required.", response.Errors);
        Assert.Contains("Last name is required.", response.Errors);
        Assert.Contains("Email format is invalid.", response.Errors);
        Assert.Contains("Phone number must be in international format, for example +254700000000.", response.Errors);
        Assert.Contains("Preferred language is required and must be a valid language code.", response.Errors);
        Assert.Contains("Country code must be a two-letter ISO code.", response.Errors);
    }

    [Fact]
    public async Task CreateAsync_WithMissingTenantContext_ReturnsFailure()
    {
        var repository = new FakeGuestRepository();
        var service = new GuestService(repository, new FakeCurrentTenantContext(null, isAuthenticated: true));

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is missing or invalid.", response.Message);
        Assert.Empty(repository.Guests);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidTenantContext_ReturnsFailure()
    {
        var repository = new FakeGuestRepository();
        var service = new GuestService(repository, new FakeCurrentTenantContext(Guid.Empty));

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is missing or invalid.", response.Message);
        Assert.Empty(repository.Guests);
    }

    [Fact]
    public async Task CreateAsync_WithoutAuthenticatedTenantContext_ReturnsFailure()
    {
        var repository = new FakeGuestRepository();
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId, isAuthenticated: false));

        var response = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is required.", response.Message);
        Assert.Empty(repository.Guests);
    }

    [Fact]
    public async Task GetByIdAsync_CrossTenantAccessReturnsNotFound()
    {
        var repository = new FakeGuestRepository();
        var guest = NewGuest(Guid.NewGuid(), "Other", "Tenant");
        repository.Guests.Add(guest);
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetByIdAsync(guest.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Guest was not found.", response.Message);
    }

    [Fact]
    public async Task UpdateAsync_CrossTenantAccessReturnsNotFound()
    {
        var repository = new FakeGuestRepository();
        var guest = NewGuest(Guid.NewGuid(), "Other", "Tenant");
        repository.Guests.Add(guest);
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.UpdateAsync(guest.Id, ValidUpdateRequest(), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Guest was not found.", response.Message);
    }

    [Fact]
    public async Task DeleteAsync_CrossTenantAccessReturnsNotFound()
    {
        var repository = new FakeGuestRepository();
        var guest = NewGuest(Guid.NewGuid(), "Other", "Tenant");
        repository.Guests.Add(guest);
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.DeleteAsync(guest.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Guest was not found.", response.Message);
        Assert.False(guest.IsDeleted);
    }

    [Fact]
    public async Task GetAsync_NeverReturnsAnotherTenantsGuests()
    {
        var repository = new FakeGuestRepository();
        repository.Guests.Add(NewGuest(repository.CompanyId, "Amina", "Mwangi"));
        repository.Guests.Add(NewGuest(Guid.NewGuid(), "Other", "Tenant"));
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetAsync(new GuestQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        var item = Assert.Single(response.Data!.Items);
        Assert.Equal("Amina", item.FirstName);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesGuestAndSetsMetadata()
    {
        var repository = new FakeGuestRepository();
        var userId = Guid.NewGuid();
        var guest = NewGuest(repository.CompanyId, "Amina", "Mwangi");
        repository.Guests.Add(guest);
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId, userId: userId));

        var response = await service.DeleteAsync(guest.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(guest.IsDeleted);
        Assert.NotNull(guest.DeletedAt);
        Assert.Equal(userId, guest.DeletedBy);
        Assert.Single(repository.AuditLogs);
    }

    [Fact]
    public async Task GetAsync_ExcludesSoftDeletedGuests()
    {
        var repository = new FakeGuestRepository();
        repository.Guests.Add(NewGuest(repository.CompanyId, "Amina", "Mwangi"));
        repository.Guests.Add(NewGuest(repository.CompanyId, "Deleted", "Guest", isDeleted: true));
        var service = new GuestService(repository, new FakeCurrentTenantContext(repository.CompanyId));

        var response = await service.GetAsync(new GuestQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        var item = Assert.Single(response.Data!.Items);
        Assert.Equal("Amina", item.FirstName);
    }

    private static CreateGuestRequest ValidCreateRequest()
    {
        return new CreateGuestRequest
        {
            FirstName = "Amina",
            LastName = "Mwangi",
            Email = "Amina@Example.COM",
            PhoneNumber = "+254700000111",
            PreferredLanguage = "SW-KE",
            CountryCode = "ke",
            Notes = "Prefers late check-in"
        };
    }

    private static UpdateGuestRequest ValidUpdateRequest()
    {
        return new UpdateGuestRequest
        {
            FirstName = "Amina",
            LastName = "Mwangi",
            Email = "amina@example.com",
            PhoneNumber = "+254700000111",
            PreferredLanguage = "en",
            CountryCode = "KE"
        };
    }

    private static Guest NewGuest(
        Guid companyId,
        string firstName,
        string lastName,
        string? email = "guest@example.com",
        string? phoneNumber = "+254700000000",
        bool isDeleted = false)
    {
        return new Guest
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true,
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

    private sealed class FakeGuestRepository : IGuestRepository
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public bool CompanyExists { get; init; } = true;
        public List<Guest> Guests { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<PagedResult<Guest>> GetAsync(Guid companyId, GuestQueryParameters query, CancellationToken cancellationToken)
        {
            var pageNumber = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;
            var guests = Guests
                .Where(guest => guest.CompanyId == companyId)
                .Where(guest => !guest.IsDeleted)
                .Where(guest => string.IsNullOrWhiteSpace(query.Search) || MatchesSearch(guest, query.Search))
                .OrderBy(guest => guest.LastName)
                .ThenBy(guest => guest.FirstName)
                .ToList();

            return Task.FromResult(new PagedResult<Guest>
            {
                Items = guests.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = guests.Count
            });
        }

        public Task<Guest?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Guests.FirstOrDefault(guest => guest.Id == id && guest.CompanyId == companyId && !guest.IsDeleted));
        }

        public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CompanyExists && companyId == CompanyId);
        }

        public Task AddAsync(Guest guest, CancellationToken cancellationToken)
        {
            Guests.Add(guest);
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

        private static bool MatchesSearch(Guest guest, string search)
        {
            return guest.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || guest.LastName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (guest.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (guest.PhoneNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
        }
    }
}
