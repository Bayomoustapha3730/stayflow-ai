using Microsoft.EntityFrameworkCore;
using StayFlow.Api.Models;

namespace StayFlow.Api.Data;

public static class SeedData
{
    public static readonly Guid DemoCompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoPropertyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().HasData(new Company
        {
            Id = DemoCompanyId,
            Name = "StayFlow Demo Hosts",
            LegalName = "StayFlow Demo Hosts Ltd",
            Email = "demo@stayflow.ai",
            PhoneNumber = "+254700000000",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });

        modelBuilder.Entity<Property>().HasData(new Property
        {
            Id = DemoPropertyId,
            CompanyId = DemoCompanyId,
            Name = "Demo Nairobi Apartment",
            AddressLine1 = "Westlands",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            Description = "Demo short-stay apartment configured for StayFlow AI onboarding.",
            IsActive = true,
            CreatedAt = SeededAt,
            UpdatedAt = SeededAt
        });
    }
}
