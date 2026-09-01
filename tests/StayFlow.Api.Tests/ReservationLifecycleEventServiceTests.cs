using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Extensions;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationLifecycleEventServiceTests
{
    [Fact]
    public async Task TryCreateAsync_CreatesPendingEventWithDeterministicIdentityAndSchedule()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedReservationGraph(dbContext);
        var service = CreateService(dbContext, utcNow: new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));

        var result = await service.TryCreateAsync(
            graph.Reservation,
            graph.Property,
            ReservationLifecycleEventType.PreArrival,
            new DateOnly(2026, 8, 10),
            CancellationToken.None);

        Assert.True(result.WasNewlyCreated);
        Assert.Equal(graph.CompanyId, result.Event.CompanyId);
        Assert.Equal(graph.Reservation.Id, result.Event.ReservationId);
        Assert.Equal(graph.Property.Id, result.Event.PropertyId);
        Assert.Equal(graph.Guest.Id, result.Event.GuestId);
        Assert.Equal(ReservationLifecycleEventType.PreArrival, result.Event.EventType);
        Assert.Equal(ReservationLifecycleRuleVersions.V1, result.Event.RuleVersion);
        Assert.Equal(new DateOnly(2026, 8, 10), result.Event.PropertyLocalDate);
        Assert.Equal(new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero), result.Event.ScheduledForUtc);
        Assert.Equal(ReservationLifecycleEventStatus.Pending, result.Event.Status);
        Assert.Equal(0, result.Event.AttemptCount);
        Assert.Null(result.Event.LastAttemptAtUtc);
        Assert.Null(result.Event.ProcessedAtUtc);
        Assert.Null(result.Event.LastError);

        var expectedKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(
            graph.CompanyId,
            graph.Reservation.Id,
            ReservationLifecycleEventType.PreArrival,
            new DateOnly(2026, 8, 10),
            ReservationLifecycleRuleVersions.V1);
        Assert.Equal(expectedKey, result.Event.IdempotencyKey);
        Assert.Single(dbContext.ReservationLifecycleEvents);
    }

    [Fact]
    public async Task TryCreateAsync_DuplicateLogicalEventReturnsExistingWithoutDuplicating()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedReservationGraph(dbContext);
        var service = CreateService(dbContext);

        var first = await service.TryCreateAsync(graph.Reservation, graph.Property, ReservationLifecycleEventType.ArrivalDay, graph.Reservation.CheckInDate, CancellationToken.None);
        var second = await service.TryCreateAsync(graph.Reservation, graph.Property, ReservationLifecycleEventType.ArrivalDay, graph.Reservation.CheckInDate, CancellationToken.None);

        Assert.True(first.WasNewlyCreated);
        Assert.False(second.WasNewlyCreated);
        Assert.Equal(first.Event.Id, second.Event.Id);
        Assert.Single(dbContext.ReservationLifecycleEvents);
    }

    [Fact]
    public async Task TryCreateAsync_SameGuestDifferentReservationsCreatesIndependentEvents()
    {
        await using var dbContext = CreateDbContext();
        var firstGraph = SeedReservationGraph(dbContext);
        var secondReservation = NewReservation(firstGraph.CompanyId, firstGraph.Property.Id, firstGraph.Guest.Id, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 14));
        dbContext.Reservations.Add(secondReservation);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var first = await service.TryCreateAsync(firstGraph.Reservation, firstGraph.Property, ReservationLifecycleEventType.PreArrival, new DateOnly(2026, 8, 10), CancellationToken.None);
        var second = await service.TryCreateAsync(secondReservation, firstGraph.Property, ReservationLifecycleEventType.PreArrival, new DateOnly(2026, 9, 10), CancellationToken.None);

        Assert.True(first.WasNewlyCreated);
        Assert.True(second.WasNewlyCreated);
        Assert.Equal(first.Event.GuestId, second.Event.GuestId);
        Assert.NotEqual(first.Event.ReservationId, second.Event.ReservationId);
        Assert.NotEqual(first.Event.IdempotencyKey, second.Event.IdempotencyKey);
        Assert.Equal(2, await dbContext.ReservationLifecycleEvents.CountAsync());
    }

    [Fact]
    public async Task TryCreateAsync_SameReservationAndEventWithDifferentRuleVersionCanCoexist()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedReservationGraph(dbContext);
        var service = CreateService(dbContext);
        var propertyLocalDate = new DateOnly(2026, 8, 10);

        await service.TryCreateAsync(graph.Reservation, graph.Property, ReservationLifecycleEventType.InStay, propertyLocalDate, CancellationToken.None);

        dbContext.ReservationLifecycleEvents.Add(new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            ReservationId = graph.Reservation.Id,
            PropertyId = graph.Property.Id,
            GuestId = graph.Guest.Id,
            EventType = ReservationLifecycleEventType.InStay,
            RuleVersion = "reservation-lifecycle-v2",
            PropertyLocalDate = propertyLocalDate,
            ScheduledForUtc = new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero),
            Status = ReservationLifecycleEventStatus.Pending,
            IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(
                graph.CompanyId,
                graph.Reservation.Id,
                ReservationLifecycleEventType.InStay,
                propertyLocalDate,
                "reservation-lifecycle-v2")
        });
        await dbContext.SaveChangesAsync();

        Assert.Equal(2, await dbContext.ReservationLifecycleEvents.CountAsync());
    }

    [Fact]
    public async Task TenantScopedOperations_DoNotFetchOrUpdateOtherTenantEvents()
    {
        await using var dbContext = CreateDbContext();
        var tenantA = SeedReservationGraph(dbContext);
        var tenantB = SeedReservationGraph(dbContext);
        var service = CreateService(dbContext);
        var tenantBEvent = await service.TryCreateAsync(tenantB.Reservation, tenantB.Property, ReservationLifecycleEventType.ArrivalDay, tenantB.Reservation.CheckInDate, CancellationToken.None);

        Assert.Null(await service.GetAsync(tenantA.CompanyId, tenantBEvent.Event.Id, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MarkProcessingAsync(tenantA.CompanyId, tenantBEvent.Event.Id, CancellationToken.None));

        var pendingForTenantA = await service.GetPendingAsync(tenantA.CompanyId, tenantBEvent.Event.ScheduledForUtc.AddMinutes(1), 10, CancellationToken.None);
        Assert.Empty(pendingForTenantA);
    }

    [Fact]
    public async Task StatusTransitions_UpdateRetryAndCompletionFields()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedReservationGraph(dbContext);
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 6, 30, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, timeProvider);
        var created = await service.TryCreateAsync(graph.Reservation, graph.Property, ReservationLifecycleEventType.CheckoutDay, graph.Reservation.CheckOutDate, CancellationToken.None);
        var createdUpdatedAt = created.Event.UpdatedAt;

        var processing = await service.MarkProcessingAsync(graph.CompanyId, created.Event.Id, CancellationToken.None);

        Assert.Equal(ReservationLifecycleEventStatus.Processing, processing.Status);
        Assert.Equal(1, processing.AttemptCount);
        Assert.Equal(timeProvider.GetUtcNow(), processing.LastAttemptAtUtc);
        Assert.True(processing.UpdatedAt >= createdUpdatedAt);

        timeProvider.UtcNow = new DateTimeOffset(2026, 8, 10, 6, 45, 0, TimeSpan.Zero);
        var processed = await service.MarkProcessedAsync(graph.CompanyId, created.Event.Id, CancellationToken.None);

        Assert.Equal(ReservationLifecycleEventStatus.Processed, processed.Status);
        Assert.Equal(timeProvider.GetUtcNow(), processed.ProcessedAtUtc);
        Assert.Null(processed.LastError);
        Assert.Equal(1, processed.AttemptCount);
    }

    [Fact]
    public async Task MarkFailed_RecordsFailureFields()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedReservationGraph(dbContext);
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 8, 10, 6, 30, 0, TimeSpan.Zero));
        var service = CreateService(dbContext, timeProvider);
        var created = await service.TryCreateAsync(graph.Reservation, graph.Property, ReservationLifecycleEventType.PostStay, graph.Reservation.CheckOutDate.AddDays(1), CancellationToken.None);
        await service.MarkProcessingAsync(graph.CompanyId, created.Event.Id, CancellationToken.None);

        timeProvider.UtcNow = new DateTimeOffset(2026, 8, 10, 6, 50, 0, TimeSpan.Zero);
        var failed = await service.MarkFailedAsync(graph.CompanyId, created.Event.Id, new string('x', 600), CancellationToken.None);

        Assert.Equal(ReservationLifecycleEventStatus.Failed, failed.Status);
        Assert.Equal(1, failed.AttemptCount);
        Assert.Equal(timeProvider.GetUtcNow(), failed.LastAttemptAtUtc);
        Assert.Equal(500, failed.LastError?.Length);
        Assert.Null(failed.ProcessedAtUtc);
    }

    [Theory]
    [InlineData("Africa/Nairobi", 2026, 8, 10, 6)]
    [InlineData("America/New_York", 2026, 3, 8, 13)]
    [InlineData("America/Los_Angeles", 2026, 8, 10, 16)]
    public async Task TryCreateAsync_ConvertsPropertyLocalScheduleToUtc(string timeZone, int year, int month, int day, int expectedUtcHour)
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedReservationGraph(dbContext, timeZone: timeZone, checkInDate: new DateOnly(year, month, day), checkOutDate: new DateOnly(year, month, day).AddDays(3));
        var service = CreateService(dbContext);

        var result = await service.TryCreateAsync(graph.Reservation, graph.Property, ReservationLifecycleEventType.ArrivalDay, graph.Reservation.CheckInDate, CancellationToken.None);

        Assert.Equal(new DateTimeOffset(year, month, day, expectedUtcHour, 0, 0, TimeSpan.Zero), result.Event.ScheduledForUtc);
    }

    [Theory]
    [InlineData(nameof(ReservationStatus.Cancelled))]
    [InlineData(nameof(ReservationStatus.NoShow))]
    public async Task TryCreateAsync_DoesNotScheduleNormalJourneyEventsForCancelledOrNoShowReservations(string statusName)
    {
        await using var dbContext = CreateDbContext();
        var status = Enum.Parse<ReservationStatus>(statusName);
        var graph = SeedReservationGraph(dbContext, reservationStatus: status);
        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TryCreateAsync(
            graph.Reservation,
            graph.Property,
            ReservationLifecycleEventType.PreArrival,
            graph.Reservation.CheckInDate,
            CancellationToken.None));
    }

    [Fact]
    public async Task TryCreateAsync_ConcurrentIdempotencyUniqueViolationReturnsExistingEvent()
    {
        var companyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyLocalDate = new DateOnly(2026, 8, 10);
        var existing = NewLifecycleEvent(companyId, reservationId, propertyId, guestId, ReservationLifecycleEventType.PreArrival, propertyLocalDate);
        var repository = new FakeReservationLifecycleEventRepository(existing, LifecycleIdempotencyUniqueViolation());
        var service = CreateService(repository);

        var result = await service.TryCreateAsync(
            NewReservation(companyId, propertyId, guestId, propertyLocalDate, propertyLocalDate.AddDays(4), reservationId),
            NewProperty(companyId, propertyId, "Africa/Nairobi"),
            ReservationLifecycleEventType.PreArrival,
            propertyLocalDate,
            CancellationToken.None);

        Assert.False(result.WasNewlyCreated);
        Assert.Equal(existing.Id, result.Event.Id);
        Assert.True(repository.DetachedFailedInsert);
    }

    [Fact]
    public async Task TryCreateAsync_UniqueViolationOnUnrelatedConstraintIsNotSwallowed()
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var propertyLocalDate = new DateOnly(2026, 8, 10);
        var repository = new FakeReservationLifecycleEventRepository(
            null,
            new DbUpdateException(
                "duplicate key",
                new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: "IX_Unrelated")));
        var service = CreateService(repository);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.TryCreateAsync(
            NewReservation(companyId, propertyId, guestId, propertyLocalDate, propertyLocalDate.AddDays(4)),
            NewProperty(companyId, propertyId, "Africa/Nairobi"),
            ReservationLifecycleEventType.PreArrival,
            propertyLocalDate,
            CancellationToken.None));
    }

    [Fact]
    public void AddApplicationServices_ResolvesLifecycleEventDependencies()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase($"lifecycle-di-{Guid.NewGuid():N}"));
        var configuration = new ConfigurationBuilder().Build();

        services.AddApplicationServices(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReservationLifecycleEventRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReservationLifecycleEventIdempotencyKeyBuilder>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReservationLifecycleEventService>());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"reservation-lifecycle-events-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ReservationLifecycleEventService CreateService(ApplicationDbContext dbContext, DateTimeOffset? utcNow = null)
    {
        return CreateService(dbContext, new MutableTimeProvider(utcNow ?? new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero)));
    }

    private static ReservationLifecycleEventService CreateService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        return new ReservationLifecycleEventService(
            new ReservationLifecycleEventRepository(dbContext),
            new ReservationLifecycleEventIdempotencyKeyBuilder(),
            timeProvider,
            Options.Create(new ReservationLifecycleEventOptions()));
    }

    private static ReservationLifecycleEventService CreateService(IReservationLifecycleEventRepository repository)
    {
        return new ReservationLifecycleEventService(
            repository,
            new ReservationLifecycleEventIdempotencyKeyBuilder(),
            new MutableTimeProvider(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero)),
            Options.Create(new ReservationLifecycleEventOptions()));
    }

    private static ReservationGraph SeedReservationGraph(
        ApplicationDbContext dbContext,
        string timeZone = "Africa/Nairobi",
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null,
        ReservationStatus reservationStatus = ReservationStatus.Confirmed)
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var company = NewCompany(companyId);
        var property = NewProperty(companyId, propertyId, timeZone);
        var guest = NewGuest(companyId, guestId);
        var reservation = NewReservation(
            companyId,
            propertyId,
            guestId,
            checkInDate ?? new DateOnly(2026, 8, 10),
            checkOutDate ?? new DateOnly(2026, 8, 14),
            status: reservationStatus);

        dbContext.Companies.Add(company);
        dbContext.Properties.Add(property);
        dbContext.Guests.Add(guest);
        dbContext.Reservations.Add(reservation);
        dbContext.SaveChanges();

        return new ReservationGraph(companyId, company, property, guest, reservation);
    }

    private static Company NewCompany(Guid companyId)
    {
        var suffix = companyId.ToString("N")[..8];
        return new Company
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
    }

    private static Property NewProperty(Guid companyId, Guid propertyId, string timeZone)
    {
        return new Property
        {
            Id = propertyId,
            CompanyId = companyId,
            Name = "Demo Property",
            AddressLine1 = "Road",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = timeZone,
            IsActive = true
        };
    }

    private static Guest NewGuest(Guid companyId, Guid guestId)
    {
        return new Guest
        {
            Id = guestId,
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };
    }

    private static Reservation NewReservation(
        Guid companyId,
        Guid propertyId,
        Guid guestId,
        DateOnly checkInDate,
        DateOnly checkOutDate,
        Guid? reservationId = null,
        ReservationStatus status = ReservationStatus.Confirmed)
    {
        return new Reservation
        {
            Id = reservationId ?? Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ReservationSource = "Manual",
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            Adults = 2,
            Children = 0,
            TotalGuestCount = 2,
            Status = status,
            IsActive = true
        };
    }

    private static ReservationLifecycleEvent NewLifecycleEvent(
        Guid companyId,
        Guid reservationId,
        Guid propertyId,
        Guid guestId,
        ReservationLifecycleEventType eventType,
        DateOnly propertyLocalDate)
    {
        return new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ReservationId = reservationId,
            PropertyId = propertyId,
            GuestId = guestId,
            EventType = eventType,
            RuleVersion = ReservationLifecycleRuleVersions.V1,
            PropertyLocalDate = propertyLocalDate,
            ScheduledForUtc = new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero),
            Status = ReservationLifecycleEventStatus.Pending,
            IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(
                companyId,
                reservationId,
                eventType,
                propertyLocalDate,
                ReservationLifecycleRuleVersions.V1)
        };
    }

    private static DbUpdateException LifecycleIdempotencyUniqueViolation() =>
        new(
            "duplicate key",
            new PostgresException(
                "duplicate key value violates unique constraint",
                "ERROR",
                "ERROR",
                PostgresErrorCodes.UniqueViolation,
                constraintName: "UX_ReservationLifecycleEvents_CompanyId_IdempotencyKey"));

    private sealed record ReservationGraph(Guid CompanyId, Company Company, Property Property, Guest Guest, Reservation Reservation);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class FakeReservationLifecycleEventRepository(
        ReservationLifecycleEvent? existingAfterSaveFailure,
        Exception saveChangesException) : IReservationLifecycleEventRepository
    {
        private ReservationLifecycleEvent? pendingInsert;
        private bool saveAttempted;

        public bool DetachedFailedInsert { get; private set; }

        public Task<ReservationLifecycleEvent?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken)
        {
            var existing = saveAttempted && existingAfterSaveFailure is not null && existingAfterSaveFailure.CompanyId == companyId && existingAfterSaveFailure.IdempotencyKey == idempotencyKey
                ? existingAfterSaveFailure
                : null;

            return Task.FromResult(existing);
        }

        public Task<ReservationLifecycleEvent?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<ReservationLifecycleEvent?>(null);
        }

        public Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<ReservationLifecycleEvent>>([]);
        }

        public Task AddAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
        {
            pendingInsert = lifecycleEvent;
            return Task.CompletedTask;
        }

        public void Detach(ReservationLifecycleEvent lifecycleEvent)
        {
            DetachedFailedInsert = ReferenceEquals(pendingInsert, lifecycleEvent);
            pendingInsert = null;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            saveAttempted = true;
            throw saveChangesException;
        }
    }
}

public sealed class ReservationLifecycleEventPostgresTests : IAsyncLifetime
{
    private const string TestPostgresConnectionVariable = "STAYFLOW_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionVariable = "ConnectionStrings__DefaultConnection";
    private const string DefaultMaintenanceConnection = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    private readonly string databaseName = $"stayflow_lifecycle_test_{Guid.NewGuid():N}";
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
        dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;

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
    public async Task ConcurrentDuplicateCreate_ResultsInExactlyOneDurableEvent()
    {
        var graph = await SeedReservationGraphAsync();
        var attempts = Enumerable.Range(0, 8)
            .Select(_ => TryCreateWithNewContextAsync(graph.Reservation, graph.Property))
            .ToArray();

        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result.WasNewlyCreated);
        Assert.Equal(7, results.Count(result => !result.WasNewlyCreated));

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        Assert.Equal(1, await verificationContext.ReservationLifecycleEvents.CountAsync(item => item.CompanyId == graph.CompanyId));
    }

    [Fact]
    public async Task DatabaseUniqueConstraint_PreventsDuplicateLogicalEvents()
    {
        var graph = await SeedReservationGraphAsync();
        var propertyLocalDate = graph.Reservation.CheckInDate;
        var idempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(
            graph.CompanyId,
            graph.Reservation.Id,
            ReservationLifecycleEventType.ArrivalDay,
            propertyLocalDate,
            ReservationLifecycleRuleVersions.V1);

        var first = InsertLifecycleEventDirectlyAsync(graph, propertyLocalDate, idempotencyKey);
        var second = InsertLifecycleEventDirectlyAsync(graph, propertyLocalDate, idempotencyKey);

        var outcomes = await Task.WhenAll(first, second);

        Assert.Single(outcomes, outcome => outcome.Inserted);
        var duplicate = Assert.Single(outcomes, outcome => !outcome.Inserted);
        Assert.Equal("UX_ReservationLifecycleEvents_CompanyId_IdempotencyKey", duplicate.ConstraintName);

        await using var verificationContext = new ApplicationDbContext(dbOptions);
        Assert.Equal(1, await verificationContext.ReservationLifecycleEvents.CountAsync(item => item.CompanyId == graph.CompanyId));
    }

    private async Task<ReservationLifecycleEventCreationResult> TryCreateWithNewContextAsync(Reservation reservation, Property property)
    {
        await using var dbContext = new ApplicationDbContext(dbOptions);
        var service = new ReservationLifecycleEventService(
            new ReservationLifecycleEventRepository(dbContext),
            new ReservationLifecycleEventIdempotencyKeyBuilder(),
            TimeProvider.System,
            Options.Create(new ReservationLifecycleEventOptions()));

        return await service.TryCreateAsync(
            reservation,
            property,
            ReservationLifecycleEventType.ArrivalDay,
            reservation.CheckInDate,
            CancellationToken.None);
    }

    private async Task<(bool Inserted, string? ConstraintName)> InsertLifecycleEventDirectlyAsync(
        ReservationGraph graph,
        DateOnly propertyLocalDate,
        string idempotencyKey)
    {
        await using var dbContext = new ApplicationDbContext(dbOptions);
        dbContext.ReservationLifecycleEvents.Add(new ReservationLifecycleEvent
        {
            Id = Guid.NewGuid(),
            CompanyId = graph.CompanyId,
            ReservationId = graph.Reservation.Id,
            PropertyId = graph.Property.Id,
            GuestId = graph.Guest.Id,
            EventType = ReservationLifecycleEventType.ArrivalDay,
            RuleVersion = ReservationLifecycleRuleVersions.V1,
            PropertyLocalDate = propertyLocalDate,
            ScheduledForUtc = new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero),
            Status = ReservationLifecycleEventStatus.Pending,
            IdempotencyKey = idempotencyKey
        });

        try
        {
            await dbContext.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return (false, postgresException.ConstraintName);
        }
    }

    private async Task<ReservationGraph> SeedReservationGraphAsync()
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var company = NewCompany(companyId);
        var property = NewProperty(companyId, propertyId);
        var guest = NewGuest(companyId, guestId);
        var reservation = NewReservation(companyId, propertyId, guestId);

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
            var builder = new NpgsqlConnectionStringBuilder(defaultConnection)
            {
                Database = "postgres"
            };

            return builder.ConnectionString;
        }

        return DefaultMaintenanceConnection;
    }

    private static Company NewCompany(Guid companyId)
    {
        var suffix = companyId.ToString("N")[..8];
        return new Company
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
    }

    private static Property NewProperty(Guid companyId, Guid propertyId)
    {
        return new Property
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
    }

    private static Guest NewGuest(Guid companyId, Guid guestId)
    {
        return new Guest
        {
            Id = guestId,
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };
    }

    private static Reservation NewReservation(Guid companyId, Guid propertyId, Guid guestId)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ReservationSource = "Manual",
            CheckInDate = new DateOnly(2026, 8, 10),
            CheckOutDate = new DateOnly(2026, 8, 14),
            Adults = 2,
            Children = 0,
            TotalGuestCount = 2,
            Status = ReservationStatus.Confirmed,
            IsActive = true
        };
    }

    private sealed record ReservationGraph(Guid CompanyId, Company Company, Property Property, Guest Guest, Reservation Reservation);
}