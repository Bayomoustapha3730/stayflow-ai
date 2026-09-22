using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Reservations;
using StayFlow.Api.Exceptions;
using StayFlow.Api.Middleware;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationQuotaInvariantTests
{
    [Fact]
    public async Task UpdateCancellationAndDeletion_DoNotChangeReservationUsage()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        var created = await harness.Service.CreateAsync(CreateRequest(harness), CancellationToken.None);
        var reservationId = Assert.IsType<ReservationDto>(created.Data).Id;
        var usageBefore = await harness.ReservationUsageAsync();
        var operationsBefore = await harness.ReservationOperationsAsync();

        var update = await harness.Service.UpdateAsync(reservationId, CreateUpdateRequest(harness), CancellationToken.None);
        var cancellation = await harness.Service.TransitionStatusAsync(
            reservationId,
            new TransitionReservationStatusRequest { TargetStatus = ReservationStatus.Cancelled.ToString() },
            CancellationToken.None);
        var deletion = await harness.Service.DeleteAsync(reservationId, CancellationToken.None);

        Assert.True(update.Success);
        Assert.True(cancellation.Success);
        Assert.True(deletion.Success);
        Assert.Equal(usageBefore, await harness.ReservationUsageAsync());
        Assert.Equal(operationsBefore, await harness.ReservationOperationsAsync());
        Assert.True(await harness.DbContext.Reservations.IgnoreQueryFilters().AnyAsync(item => item.Id == reservationId && item.IsDeleted));
    }

    [Fact]
    public async Task UnlimitedReservationQuota_AllowsAdmissionAboveFiniteThreshold()
    {
        var harness = await CreateHarnessAsync(limit: null, unlimited: true);
        harness.DbContext.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = harness.CompanyId,
            Metric = UsageMetric.Reservations.ToStorageValue(),
            PeriodStartUtc = harness.PeriodStartUtc,
            PeriodEndUtc = harness.PeriodEndUtc,
            QuantityUsed = 100
        });
        await harness.DbContext.SaveChangesAsync();

        var response = await harness.Service.CreateAsync(CreateRequest(harness), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(101, await harness.ReservationUsageAsync());
        Assert.True((await harness.DbContext.Reservations.CountAsync()) >= 1);
    }

    [Fact]
    public async Task ReservationQuota_IsolatedBetweenTenants()
    {
        var databaseName = $"reservation-isolation-{Guid.NewGuid():N}";
        var first = await CreateHarnessAsync(limit: 1, databaseName: databaseName);
        var second = await CreateHarnessAsync(limit: 1, databaseName: databaseName);
        await first.Service.CreateAsync(CreateRequest(first), CancellationToken.None);

        var secondResponse = await second.Service.CreateAsync(CreateRequest(second), CancellationToken.None);

        Assert.True(secondResponse.Success);
        Assert.Equal(1, await first.ReservationUsageAsync());
        Assert.Equal(1, await second.ReservationUsageAsync());
        Assert.Equal(1, await first.ReservationOperationsAsync());
        Assert.Equal(1, await second.ReservationOperationsAsync());
    }

    [Fact]
    public async Task DistinctReservations_ConsumeDistinctOperations()
    {
        var harness = await CreateHarnessAsync(limit: 2);

        var first = await harness.Service.CreateAsync(CreateRequest(harness), CancellationToken.None);
        var second = await harness.Service.CreateAsync(CreateRequest(harness), CancellationToken.None);
        var firstId = Assert.IsType<ReservationDto>(first.Data).Id;
        var secondId = Assert.IsType<ReservationDto>(second.Data).Id;
        var keys = await harness.DbContext.UsageOperations
            .Where(operation => operation.Metric == UsageMetric.Reservations.ToStorageValue())
            .Select(operation => operation.IdempotencyKey)
            .ToListAsync();

        var replay = await harness.EntitlementService.AdmitReservationAsync(
            harness.CompanyId,
            firstId,
            _ => throw new InvalidOperationException("A replay must not persist a second reservation."),
            CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(2, await harness.ReservationUsageAsync());
        Assert.Equal(2, keys.Count);
        Assert.Contains($"reservation:{firstId:N}", keys);
        Assert.Contains($"reservation:{secondId:N}", keys);
        Assert.True(replay.WasIdempotentReplay);
        Assert.Equal(2, await harness.ReservationOperationsAsync());
    }

    [Fact]
    public async Task PriorPeriodReservationUsage_IsIgnoredForCurrentAdmission()
    {
        var harness = await CreateHarnessAsync(limit: 1);
        harness.DbContext.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = harness.CompanyId,
            Metric = UsageMetric.Reservations.ToStorageValue(),
            PeriodStartUtc = harness.PeriodStartUtc.AddMonths(-1),
            PeriodEndUtc = harness.PeriodEndUtc.AddMonths(-1),
            QuantityUsed = 99
        });
        await harness.DbContext.SaveChangesAsync();

        var response = await harness.Service.CreateAsync(CreateRequest(harness), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(1, await harness.ReservationUsageAsync());
        Assert.Equal(99, await harness.DbContext.UsageRecords
            .Where(record => record.Metric == UsageMetric.Reservations.ToStorageValue() && record.PeriodStartUtc == harness.PeriodStartUtc.AddMonths(-1))
            .Select(record => record.QuantityUsed)
            .SingleAsync());
    }

    [Fact]
    public async Task ReservationSnapshot_UsesCurrentUsageRecordNotReservationRowCount()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        harness.DbContext.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = harness.CompanyId,
            Metric = UsageMetric.Reservations.ToStorageValue(),
            PeriodStartUtc = harness.PeriodStartUtc,
            PeriodEndUtc = harness.PeriodEndUtc,
            QuantityUsed = 3
        });
        await harness.DbContext.SaveChangesAsync();

        var snapshot = await harness.EntitlementService.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None);
        var quota = Assert.Single(snapshot.Quotas.Where(item => item.Metric == UsageMetric.Reservations));

        Assert.Equal(3, quota.Used);
        Assert.Equal(2, quota.Remaining);
        Assert.Equal(0, await harness.DbContext.Reservations.CountAsync());
    }

    [Fact]
    public async Task DowngradeBelowCurrentReservationUsage_PreservesExistingAndRejectsNewAdmission()
    {
        var harness = await CreateHarnessAsync(limit: 5);
        var existing = await harness.Service.CreateAsync(CreateRequest(harness), CancellationToken.None);
        var existingId = Assert.IsType<ReservationDto>(existing.Data).Id;
        var entitlement = await harness.DbContext.PlanEntitlements
            .SingleAsync(item => item.Key == UsageMetric.Reservations.ToQuotaEntitlementKey());
        entitlement.QuotaLimit = 0;
        await harness.DbContext.SaveChangesAsync();

        var snapshot = await harness.EntitlementService.GetCurrentSnapshotAsync(harness.CompanyId, CancellationToken.None);
        var rejected = await Assert.ThrowsAsync<QuotaExceededException>(() =>
            harness.Service.CreateAsync(CreateRequest(harness), CancellationToken.None));

        var quota = Assert.Single(snapshot.Quotas.Where(item => item.Metric == UsageMetric.Reservations));
        Assert.Equal(1, quota.Used);
        Assert.Equal(0, quota.Remaining);
        Assert.Equal(1, await harness.DbContext.Reservations.CountAsync());
        Assert.True(await harness.DbContext.Reservations.AnyAsync(item => item.Id == existingId));
        Assert.Equal(1, await harness.ReservationUsageAsync());
        Assert.Equal(1, await harness.ReservationOperationsAsync());
        Assert.Equal(UsageMetric.Reservations.ToStorageValue(), rejected.Metric);
    }

    [Fact]
    public async Task QuotaExceptionHandler_Returns429QuotaExceededContract()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            new FakeHostEnvironment(Environments.Production));

        var handled = await handler.TryHandleAsync(
            context,
            new QuotaExceededException(UsageMetric.Reservations.ToStorageValue(), 1, 1, 1),
            CancellationToken.None);

        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal("quota_exceeded", document.RootElement.GetProperty("errorCode").GetString());
    }

    private static CreateReservationRequest CreateRequest(Harness harness)
    {
        return new CreateReservationRequest
        {
            PropertyId = harness.PropertyId,
            PrimaryGuestId = harness.GuestId,
            ReservationSource = "Manual",
            CheckInDate = new DateOnly(2026, 10, 1),
            CheckOutDate = new DateOnly(2026, 10, 3),
            Adults = 2,
            Children = 0,
            Currency = "KES",
            BookingAmount = 100
        };
    }

    private static UpdateReservationRequest CreateUpdateRequest(Harness harness)
    {
        return new UpdateReservationRequest
        {
            PropertyId = harness.PropertyId,
            PrimaryGuestId = harness.GuestId,
            ReservationSource = "Manual",
            CheckInDate = new DateOnly(2026, 10, 2),
            CheckOutDate = new DateOnly(2026, 10, 4),
            Adults = 2,
            Children = 1,
            Currency = "KES",
            BookingAmount = 150,
            IsActive = true
        };
    }

    private static async Task<Harness> CreateHarnessAsync(long? limit, bool unlimited = false, string? databaseName = null)
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var periodStartUtc = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEndUtc = periodStartUtc.AddMonths(1).AddTicks(-1);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"reservation-quota-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new ApplicationDbContext(options, new HarnessTenantContext(companyId));

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = $"Reservation Tenant {companyId:N}",
            Slug = $"reservation-{companyId:N}",
            NormalizedSlug = $"RESERVATION-{companyId:N}",
            Status = "Active",
            Email = $"{companyId:N}@reservation.test",
            PhoneNumber = "+254700000000",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        dbContext.Properties.Add(new Property
        {
            Id = propertyId,
            CompanyId = companyId,
            Name = "Test Property",
            AddressLine1 = "Test Address",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        dbContext.Guests.Add(new Guest
        {
            Id = guestId,
            CompanyId = companyId,
            FirstName = "Test",
            LastName = "Guest",
            CountryCode = "KE",
            IsActive = true
        });
        dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Id = planId,
            Name = $"ReservationTest-{planId:N}",
            DisplayName = "Reservation Test",
            Description = "Reservation quota test plan",
            IsActive = true,
            IsEnterprise = unlimited,
            Entitlements =
            [
                new PlanEntitlement
                {
                    Id = Guid.NewGuid(),
                    Key = UsageMetric.Reservations.ToQuotaEntitlementKey(),
                    IsEnabled = true,
                    QuotaLimit = limit,
                    IsUnlimited = unlimited,
                    Unit = "count"
                }
            ]
        });
        dbContext.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionPlanId = planId,
            Status = SubscriptionStatus.Active.ToStorageValue(),
            CurrentPeriodStartUtc = periodStartUtc,
            CurrentPeriodEndUtc = periodEndUtc
        });
        await dbContext.SaveChangesAsync();

        var entitlementService = new SubscriptionEntitlementService(dbContext, NullLogger<SubscriptionEntitlementService>.Instance);
        var reservationService = new ReservationService(
            new ReservationRepository(dbContext),
            new HarnessTenantContext(companyId),
            new ReservationStatusTransitionPolicy(),
            entitlementService);
        return new Harness(dbContext, entitlementService, reservationService, companyId, propertyId, guestId, periodStartUtc, periodEndUtc);
    }

    private sealed class Harness(
        ApplicationDbContext dbContext,
        SubscriptionEntitlementService entitlementService,
        ReservationService service,
        Guid companyId,
        Guid propertyId,
        Guid guestId,
        DateTimeOffset periodStartUtc,
        DateTimeOffset periodEndUtc)
    {
        public ApplicationDbContext DbContext { get; } = dbContext;
        public SubscriptionEntitlementService EntitlementService { get; } = entitlementService;
        public ReservationService Service { get; } = service;
        public Guid CompanyId { get; } = companyId;
        public Guid PropertyId { get; } = propertyId;
        public Guid GuestId { get; } = guestId;
        public DateTimeOffset PeriodStartUtc { get; } = periodStartUtc;
        public DateTimeOffset PeriodEndUtc { get; } = periodEndUtc;

        public Task<long> ReservationUsageAsync()
        {
            return DbContext.UsageRecords
                .Where(record => record.CompanyId == CompanyId
                    && record.Metric == UsageMetric.Reservations.ToStorageValue()
                    && record.PeriodStartUtc == PeriodStartUtc)
                .Select(record => (long?)record.QuantityUsed)
                .SingleOrDefaultAsync()
                .ContinueWith(task => task.Result ?? 0L);
        }

        public Task<int> ReservationOperationsAsync()
        {
            return DbContext.UsageOperations.CountAsync(operation => operation.CompanyId == CompanyId
                && operation.Metric == UsageMetric.Reservations.ToStorageValue()
                && operation.PeriodStartUtc == PeriodStartUtc);
        }
    }

    private sealed class HarnessTenantContext(Guid companyId) : ITenantContext, ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? TenantId => CompanyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId => "reservation-quota-test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "StayFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
