using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Data;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ApplicationDbContextTenantWriteValidationTests
{
    [Fact]
    public async Task SaveChangesAsync_BlocksCrossTenantCompanyIdAssignment_WhenAuthenticated()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tenant-write-{Guid.NewGuid():N}")
            .Options;

        var tenantCompanyId = Guid.NewGuid();
        await using var dbContext = new ApplicationDbContext(options, new FakeTenantContext(tenantCompanyId, Guid.NewGuid(), true));

        dbContext.Properties.Add(new Property
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            Name = "Cross Tenant Property",
            AddressLine1 = "Address",
            City = "City",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => dbContext.SaveChangesAsync());
        Assert.Equal("tenant_write_mismatch", exception.ErrorCode);
    }

    [Fact]
    public async Task SaveChangesAsync_BlocksCrossTenantForeignKeys_WhenAuthenticated()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tenant-fk-{Guid.NewGuid():N}")
            .Options;

        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var guestA = Guid.NewGuid();
        var propertyB = Guid.NewGuid();

        await using (var seedContext = new ApplicationDbContext(options))
        {
            seedContext.Companies.AddRange(
                new Company
                {
                    Id = companyA,
                    Name = "Company A",
                    Slug = "company-a",
                    NormalizedSlug = "COMPANY-A",
                    Status = "Active",
                    Email = "a@example.com",
                    PhoneNumber = "+254700000001",
                    CountryCode = "KE",
                    TimeZone = "Africa/Nairobi",
                    IsActive = true
                },
                new Company
                {
                    Id = companyB,
                    Name = "Company B",
                    Slug = "company-b",
                    NormalizedSlug = "COMPANY-B",
                    Status = "Active",
                    Email = "b@example.com",
                    PhoneNumber = "+254700000002",
                    CountryCode = "KE",
                    TimeZone = "Africa/Nairobi",
                    IsActive = true
                });

            seedContext.Properties.Add(new Property
            {
                Id = propertyB,
                CompanyId = companyB,
                Name = "Foreign Property",
                AddressLine1 = "Address",
                City = "City",
                CountryCode = "KE",
                TimeZone = "Africa/Nairobi",
                IsActive = true
            });

            seedContext.Guests.Add(new Guest
            {
                Id = guestA,
                CompanyId = companyA,
                FirstName = "Guest",
                LastName = "A",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            });

            await seedContext.SaveChangesAsync();
        }

        await using var dbContext = new ApplicationDbContext(options, new FakeTenantContext(companyA, Guid.NewGuid(), true));
        dbContext.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyA,
            PropertyId = propertyB,
            PrimaryGuestId = guestA,
            CheckInDate = new DateOnly(2026, 8, 10),
            CheckOutDate = new DateOnly(2026, 8, 12),
            ReservationSource = "Manual",
            Status = ReservationStatus.Draft,
            IsActive = true
        });

        var exception = await Assert.ThrowsAsync<DomainValidationException>(() => dbContext.SaveChangesAsync());
        Assert.Equal("tenant_foreign_key_mismatch", exception.ErrorCode);
    }

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId, bool isAuthenticated) : ITenantContext
    {
        public Guid? TenantId => companyId;
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => null;
        public bool IsAuthenticated { get; } = isAuthenticated;
    }
}