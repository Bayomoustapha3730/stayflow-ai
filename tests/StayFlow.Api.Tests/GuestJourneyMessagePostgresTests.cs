using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

/// <summary>
/// Real, ephemeral PostgreSQL coverage for the two mandatory Slice 5 concurrency guarantees:
/// exactly-once GuestJourneyMessage creation per lifecycle event, and exactly-once ClaimDueAsync
/// delivery claiming. EF InMemory cannot enforce unique constraints or FOR UPDATE SKIP LOCKED, so
/// both guarantees must be proven against a real database.
/// </summary>
public sealed class GuestJourneyMessagePostgresTests : IAsyncLifetime
{
    private const string TestPostgresConnectionVariable = "STAYFLOW_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionVariable = "ConnectionStrings__DefaultConnection";
    private const string DefaultMaintenanceConnection = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    private readonly string databaseName = $"stayflow_guest_journey_test_{Guid.NewGuid():N}";
    private string maintenanceConnectionString = string.Empty;
    private DbContextOptions<ApplicationDbContext> dbOptions = null!;

    public async Task InitializeAsync()
    {
        maintenanceConnectionString = ResolveMaintenanceConnectionString();

        await using (var maintenanceConnection = new NpgsqlConnection(maintenanceConnectionString))
        {
            await maintenanceConnection.OpenAsync();
            await using var createCommand = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", maintenanceConnection);
            await createCommand.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(maintenanceConnectionString) { Database = databaseName };
        dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(builder.ConnectionString).Options;

        await using var schemaContext = new ApplicationDbContext(dbOptions);
        await schemaContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var maintenanceConnection = new NpgsqlConnection(maintenanceConnectionString);
        await maintenanceConnection.OpenAsync();

        await using (var terminateCommand = new NpgsqlCommand(
            $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{databaseName}'",
            maintenanceConnection))
        {
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", maintenanceConnection);
        await dropCommand.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task TryCreateAsync_ConcurrentSameLifecycleEvent_CreatesExactlyOneGuestJourneyMessage()
    {
        var graph = await SeedGraphAsync();
        var lifecycleEventId = await SeedLifecycleEventAsync(graph, ReservationLifecycleEventType.ArrivalDay, graph.Reservation.CheckInDate);

        var attempts = Enumerable.Range(0, 8)
            .Select(_ => TryCreateWithNewContextAsync(graph.CompanyId, lifecycleEventId))
            .ToArray();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.WasNewlyCreated);
        Assert.Equal(7, results.Count(result => !result.WasNewlyCreated));

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        Assert.Equal(1, await verificationContext.GuestJourneyMessages.CountAsync(item => item.ReservationLifecycleEventId == lifecycleEventId));
    }

    [Fact]
    public async Task ConcurrentClaimDueAsync_ReturnsEachMessageToAtMostOneClaimer()
    {
        var graph = await SeedGraphAsync();
        var nowUtc = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var seededIds = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            var lifecycleEventId = await SeedLifecycleEventAsync(graph, (ReservationLifecycleEventType)(i % 5), graph.Reservation.CheckInDate.AddDays(i));
            seededIds.Add(await SeedGuestJourneyMessageAsync(graph, lifecycleEventId, nextAttemptAtUtc: nowUtc.AddMinutes(-i - 1)));
        }

        var first = ClaimWithNewContextAsync(nowUtc, 6);
        var second = ClaimWithNewContextAsync(nowUtc, 6);
        var claimed = (await Task.WhenAll(first, second)).SelectMany(item => item).ToList();

        Assert.Equal(6, claimed.Count);
        Assert.Equal(claimed.Count, claimed.Select(item => item.Id).Distinct().Count());
        Assert.All(claimed, item => Assert.Contains(item.Id, seededIds));
    }

    [Fact]
    public async Task ClaimDueAsync_ClaimsDuePendingOnlyAndHonorsBatchSize()
    {
        var graph = await SeedGraphAsync();
        var nowUtc = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

        var dueIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var dueEventId = await SeedLifecycleEventAsync(graph, (ReservationLifecycleEventType)i, graph.Reservation.CheckInDate.AddDays(i));
            dueIds.Add(await SeedGuestJourneyMessageAsync(graph, dueEventId, nextAttemptAtUtc: nowUtc.AddMinutes(-1)));
        }

        var futureEventId = await SeedLifecycleEventAsync(graph, ReservationLifecycleEventType.PostStay, graph.Reservation.CheckOutDate.AddDays(1));
        var futureId = await SeedGuestJourneyMessageAsync(graph, futureEventId, nextAttemptAtUtc: nowUtc.AddMinutes(10));

        await using var dbContext = new ApplicationDbContext(dbOptions);
        var repository = new GuestJourneyMessageRepository(dbContext);
        var claimed = await repository.ClaimDueAsync(nowUtc, 2, CancellationToken.None);

        Assert.Equal(2, claimed.Count);
        Assert.All(claimed, message =>
        {
            Assert.Contains(message.Id, dueIds);
            Assert.NotEqual(futureId, message.Id);
            Assert.Equal(GuestJourneyMessageStatus.Processing, message.Status);
            Assert.Equal(1, message.AttemptCount);
            Assert.Equal(nowUtc, message.LastAttemptAtUtc);
        });

        // The remaining due message is claimable; the future-dated one still is not.
        var remaining = await repository.ClaimDueAsync(nowUtc, 10, CancellationToken.None);
        var remainingMessage = Assert.Single(remaining);
        Assert.Contains(remainingMessage.Id, dueIds);
        Assert.NotEqual(futureId, remainingMessage.Id);
    }

    [Fact]
    public async Task Recovery_RestoresStaleProcessingAndRetryableFailedButHonorsFreshAndMaxAttempts()
    {
        var graph = await SeedGraphAsync();
        var nowUtc = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

        var staleEventId = await SeedLifecycleEventAsync(graph, ReservationLifecycleEventType.PreArrival, graph.Reservation.CheckInDate.AddDays(-7));
        var staleId = await SeedGuestJourneyMessageAsync(graph, staleEventId, GuestJourneyMessageStatus.Processing, attemptCount: 1, lastAttemptAtUtc: nowUtc.AddMinutes(-30));

        var freshEventId = await SeedLifecycleEventAsync(graph, ReservationLifecycleEventType.ArrivalDay, graph.Reservation.CheckInDate);
        var freshId = await SeedGuestJourneyMessageAsync(graph, freshEventId, GuestJourneyMessageStatus.Processing, attemptCount: 1, lastAttemptAtUtc: nowUtc.AddMinutes(-2));

        var retryableEventId = await SeedLifecycleEventAsync(graph, ReservationLifecycleEventType.CheckoutDay, graph.Reservation.CheckOutDate);
        var retryableId = await SeedGuestJourneyMessageAsync(graph, retryableEventId, GuestJourneyMessageStatus.Failed, attemptCount: 1, nextAttemptAtUtc: nowUtc.AddMinutes(-1));

        var maxedEventId = await SeedLifecycleEventAsync(graph, ReservationLifecycleEventType.PostStay, graph.Reservation.CheckOutDate.AddDays(1));
        var maxedId = await SeedGuestJourneyMessageAsync(graph, maxedEventId, GuestJourneyMessageStatus.Failed, attemptCount: 5, nextAttemptAtUtc: nowUtc.AddMinutes(-1));

        var futureRetryEventId = await SeedLifecycleEventAsync(graph, ReservationLifecycleEventType.InStay, graph.Reservation.CheckInDate.AddDays(1));
        var futureRetryId = await SeedGuestJourneyMessageAsync(graph, futureRetryEventId, GuestJourneyMessageStatus.Failed, attemptCount: 1, nextAttemptAtUtc: nowUtc.AddMinutes(10));

        await using var dbContext = new ApplicationDbContext(dbOptions);
        var repository = new GuestJourneyMessageRepository(dbContext);

        Assert.Equal(1, await repository.RecoverStaleProcessingAsync(nowUtc.AddMinutes(-15), nowUtc, CancellationToken.None));
        Assert.Equal(1, await repository.RecoverRetryableFailedAsync(nowUtc, 5, CancellationToken.None));

        var claimed = await repository.ClaimDueAsync(nowUtc, 10, CancellationToken.None);

        Assert.Contains(claimed, item => item.Id == staleId);
        Assert.Contains(claimed, item => item.Id == retryableId);
        Assert.DoesNotContain(claimed, item => item.Id == freshId || item.Id == maxedId || item.Id == futureRetryId);
    }

    private async Task<GuestJourneyMessageCreationResult> TryCreateWithNewContextAsync(Guid companyId, Guid lifecycleEventId)
    {
        await using var dbContext = new ApplicationDbContext(dbOptions);
        var lifecycleEvent = await dbContext.ReservationLifecycleEvents.SingleAsync(item => item.CompanyId == companyId && item.Id == lifecycleEventId);
        var service = new GuestJourneyMessageService(new GuestJourneyMessageRepository(dbContext));
        return await service.TryCreateAsync(lifecycleEvent, "en", "Hi Ada, today is your check-in day at Demo Property.", null, CancellationToken.None);
    }

    private async Task<IReadOnlyCollection<GuestJourneyMessage>> ClaimWithNewContextAsync(DateTimeOffset nowUtc, int batchSize)
    {
        await using var dbContext = new ApplicationDbContext(dbOptions);
        return await new GuestJourneyMessageRepository(dbContext).ClaimDueAsync(nowUtc, batchSize, CancellationToken.None);
    }

    private async Task<Guid> SeedLifecycleEventAsync(ReservationGraph graph, ReservationLifecycleEventType eventType, DateOnly propertyLocalDate)
    {
        var lifecycleEvent = new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            ReservationId = graph.Reservation.Id,
            PropertyId = graph.Property.Id,
            GuestId = graph.Guest.Id,
            EventType = eventType,
            RuleVersion = ReservationLifecycleRuleVersions.V1,
            PropertyLocalDate = propertyLocalDate,
            ScheduledForUtc = new DateTimeOffset(propertyLocalDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            Status = ReservationLifecycleEventStatus.Processing,
            IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(graph.CompanyId, graph.Reservation.Id, eventType, propertyLocalDate, ReservationLifecycleRuleVersions.V1)
        };

        await using var dbContext = new ApplicationDbContext(dbOptions);
        dbContext.ReservationLifecycleEvents.Add(lifecycleEvent);
        await dbContext.SaveChangesAsync();
        return lifecycleEvent.Id;
    }

    private async Task<Guid> SeedGuestJourneyMessageAsync(
        ReservationGraph graph,
        Guid lifecycleEventId,
        GuestJourneyMessageStatus status = GuestJourneyMessageStatus.Pending,
        int attemptCount = 0,
        DateTimeOffset? lastAttemptAtUtc = null,
        DateTimeOffset? nextAttemptAtUtc = null)
    {
        var message = new GuestJourneyMessage
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            ReservationId = graph.Reservation.Id,
            ReservationLifecycleEventId = lifecycleEventId,
            PropertyId = graph.Property.Id,
            GuestId = graph.Guest.Id,
            JourneyEventType = ReservationLifecycleEventType.ArrivalDay,
            Language = "en",
            RenderedContent = "Hi Ada, today is your check-in day at Demo Property.",
            Status = status,
            AttemptCount = attemptCount,
            LastAttemptAtUtc = lastAttemptAtUtc,
            NextAttemptAtUtc = nextAttemptAtUtc,
            IdempotencyKey = $"guest-journey:{lifecycleEventId:N}"
        };

        await using var dbContext = new ApplicationDbContext(dbOptions);
        dbContext.GuestJourneyMessages.Add(message);
        await dbContext.SaveChangesAsync();
        return message.Id;
    }

    private async Task<ReservationGraph> SeedGraphAsync()
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var suffix = companyId.ToString("N")[..8];
        var company = new Company
        {
            Id = companyId,
            Name = $"Company {suffix}",
            Slug = $"company-{suffix}",
            NormalizedSlug = $"COMPANY-{suffix}".ToUpperInvariant(),
            Status = "Active",
            Email = $"{suffix}@example.com",
            PhoneNumber = "+254700000001",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        };
        var property = new Property
        {
            Id = propertyId,
            CompanyId = companyId,
            Name = "Demo Property",
            AddressLine1 = "Road",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        };
        var guest = new Guest
        {
            Id = guestId,
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ReservationSource = "Manual",
            CheckInDate = new DateOnly(2026, 8, 10),
            CheckOutDate = new DateOnly(2026, 8, 14),
            Adults = 1,
            Status = ReservationStatus.Confirmed,
            IsActive = true
        };

        await using var dbContext = new ApplicationDbContext(dbOptions);
        dbContext.Companies.Add(company);
        dbContext.Properties.Add(property);
        dbContext.Guests.Add(guest);
        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync();

        return new ReservationGraph(companyId, company, property, guest, reservation);
    }

    private static string ResolveMaintenanceConnectionString()
    {
        var explicitConnection = Environment.GetEnvironmentVariable(TestPostgresConnectionVariable);
        if (!string.IsNullOrWhiteSpace(explicitConnection))
        {
            return explicitConnection;
        }

        var defaultConnection = Environment.GetEnvironmentVariable(DefaultConnectionVariable);
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            return new NpgsqlConnectionStringBuilder(defaultConnection) { Database = "postgres" }.ConnectionString;
        }

        return DefaultMaintenanceConnection;
    }

    private sealed record ReservationGraph(Guid CompanyId, Company Company, Property Property, Guest Guest, Reservation Reservation);
}
