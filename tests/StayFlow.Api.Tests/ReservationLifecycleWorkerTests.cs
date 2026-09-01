using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationLifecycleEventGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_EligibleReservationCreatesExpectedAnchoredEvents()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext, checkInDate: new DateOnly(2026, 8, 10), checkOutDate: new DateOnly(2026, 8, 14));
        var generator = CreateGenerator(dbContext, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));

        var created = await generator.GenerateAsync(CancellationToken.None);

        Assert.Equal(5, created);
        var events = await dbContext.ReservationLifecycleEvents.OrderBy(item => item.PropertyLocalDate).ThenBy(item => item.EventType).ToListAsync();
        Assert.Contains(events, item => item.EventType == ReservationLifecycleEventType.PreArrival && item.PropertyLocalDate == new DateOnly(2026, 8, 3));
        Assert.Contains(events, item => item.EventType == ReservationLifecycleEventType.ArrivalDay && item.PropertyLocalDate == new DateOnly(2026, 8, 10));
        Assert.Contains(events, item => item.EventType == ReservationLifecycleEventType.InStay && item.PropertyLocalDate == new DateOnly(2026, 8, 11));
        Assert.Contains(events, item => item.EventType == ReservationLifecycleEventType.CheckoutDay && item.PropertyLocalDate == new DateOnly(2026, 8, 14));
        Assert.Contains(events, item => item.EventType == ReservationLifecycleEventType.PostStay && item.PropertyLocalDate == new DateOnly(2026, 8, 15));
        Assert.All(events, item => Assert.Equal(graph.CompanyId, item.CompanyId));
    }

    [Fact]
    public async Task GenerateAsync_OneNightReservationDoesNotCreateInStayEvent()
    {
        await using var dbContext = CreateDbContext();
        SeedGraph(dbContext, checkInDate: new DateOnly(2026, 8, 10), checkOutDate: new DateOnly(2026, 8, 11));
        var generator = CreateGenerator(dbContext, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));

        await generator.GenerateAsync(CancellationToken.None);

        Assert.Equal(4, await dbContext.ReservationLifecycleEvents.CountAsync());
        Assert.DoesNotContain(dbContext.ReservationLifecycleEvents, item => item.EventType == ReservationLifecycleEventType.InStay);
    }

    [Fact]
    public async Task GenerateAsync_RepeatedPassDoesNotDuplicateEvents()
    {
        await using var dbContext = CreateDbContext();
        SeedGraph(dbContext);
        var generator = CreateGenerator(dbContext, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(5, await generator.GenerateAsync(CancellationToken.None));
        Assert.Equal(0, await generator.GenerateAsync(CancellationToken.None));

        Assert.Equal(5, await dbContext.ReservationLifecycleEvents.CountAsync());
    }

    [Theory]
    [InlineData(nameof(ReservationStatus.Draft))]
    [InlineData(nameof(ReservationStatus.PendingConfirmation))]
    [InlineData(nameof(ReservationStatus.Cancelled))]
    [InlineData(nameof(ReservationStatus.NoShow))]
    public async Task GenerateAsync_IneligibleReservationStatusesAreExcluded(string statusName)
    {
        await using var dbContext = CreateDbContext();
        SeedGraph(dbContext, status: Enum.Parse<ReservationStatus>(statusName));
        var generator = CreateGenerator(dbContext, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, await generator.GenerateAsync(CancellationToken.None));
        Assert.Empty(dbContext.ReservationLifecycleEvents);
    }

    [Fact]
    public async Task GenerateAsync_ExcludesReservationsOutsideLookbackAndHorizon()
    {
        await using var dbContext = CreateDbContext();
        SeedGraph(dbContext, checkInDate: new DateOnly(2026, 7, 1), checkOutDate: new DateOnly(2026, 7, 5));
        SeedGraph(dbContext, checkInDate: new DateOnly(2026, 10, 1), checkOutDate: new DateOnly(2026, 10, 5));
        var generator = CreateGenerator(dbContext, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, await generator.GenerateAsync(CancellationToken.None));
        Assert.Empty(dbContext.ReservationLifecycleEvents);
    }

    [Fact]
    public async Task GenerateAsync_SameGuestMultipleReservationsAndCompaniesRemainIndependent()
    {
        await using var dbContext = CreateDbContext();
        var first = SeedGraph(dbContext, checkInDate: new DateOnly(2026, 8, 10), checkOutDate: new DateOnly(2026, 8, 12));
        var secondReservation = NewReservation(first.CompanyId, first.Property.Id, first.Guest.Id, new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 22));
        secondReservation.Property = first.Property;
        secondReservation.PrimaryGuest = first.Guest;
        dbContext.Reservations.Add(secondReservation);
        SeedGraph(dbContext, checkInDate: new DateOnly(2026, 8, 10), checkOutDate: new DateOnly(2026, 8, 12));
        await dbContext.SaveChangesAsync();
        var generator = CreateGenerator(dbContext, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));

        await generator.GenerateAsync(CancellationToken.None);

        Assert.Equal(15, await dbContext.ReservationLifecycleEvents.CountAsync());
        Assert.Equal(10, await dbContext.ReservationLifecycleEvents.CountAsync(item => item.CompanyId == first.CompanyId));
        Assert.Equal(10, await dbContext.ReservationLifecycleEvents.CountAsync(item => item.GuestId == first.Guest.Id));
    }

    [Fact]
    public async Task GenerateAsync_PropertyTimezoneAndConfiguredPreArrivalWindowAreRespected()
    {
        await using var dbContext = CreateDbContext();
        SeedGraph(dbContext, timeZone: "America/Los_Angeles", checkInDate: new DateOnly(2026, 8, 10), checkOutDate: new DateOnly(2026, 8, 12));
        var generator = CreateGenerator(
            dbContext,
            new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero),
            preArrivalWindowDays: 3);

        await generator.GenerateAsync(CancellationToken.None);

        var preArrival = await dbContext.ReservationLifecycleEvents.SingleAsync(item => item.EventType == ReservationLifecycleEventType.PreArrival);
        Assert.Equal(new DateOnly(2026, 8, 7), preArrival.PropertyLocalDate);
        Assert.Equal(new DateTimeOffset(2026, 8, 7, 16, 0, 0, TimeSpan.Zero), preArrival.ScheduledForUtc);
    }

    [Fact]
    public async Task GenerateAsync_DateChangeSuppressesObsoleteUnprocessedEventsAndCreatesCurrentAnchors()
    {
        await using var dbContext = CreateDbContext();
        var graph = SeedGraph(dbContext, checkInDate: new DateOnly(2026, 8, 10), checkOutDate: new DateOnly(2026, 8, 12));
        var generator = CreateGenerator(dbContext, new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));
        await generator.GenerateAsync(CancellationToken.None);

        graph.Reservation.CheckInDate = new DateOnly(2026, 8, 20);
        graph.Reservation.CheckOutDate = new DateOnly(2026, 8, 22);
        await dbContext.SaveChangesAsync();
        var createdAfterDateChange = await generator.GenerateAsync(CancellationToken.None);

        Assert.Equal(5, createdAfterDateChange);
        Assert.Equal(5, await dbContext.ReservationLifecycleEvents.CountAsync(item => item.Status == ReservationLifecycleEventStatus.Suppressed));
        Assert.Equal(5, await dbContext.ReservationLifecycleEvents.CountAsync(item => item.Status == ReservationLifecycleEventStatus.Pending));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"reservation-lifecycle-generator-{Guid.NewGuid():N}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ReservationLifecycleEventGenerator CreateGenerator(ApplicationDbContext dbContext, DateTimeOffset utcNow, int preArrivalWindowDays = 7)
    {
        return new ReservationLifecycleEventGenerator(
            new ReservationLifecycleEventRepository(dbContext),
            new ReservationLifecycleEventService(new ReservationLifecycleEventRepository(dbContext), new ReservationLifecycleEventIdempotencyKeyBuilder(), new MutableTimeProvider(utcNow), Options.Create(new ReservationLifecycleEventOptions())),
            new ReservationLifecycleEventIdempotencyKeyBuilder(),
            new MutableTimeProvider(utcNow),
            Options.Create(new ReservationLifecycleEventOptions()),
            Options.Create(new ReservationContextOptions { PreArrivalWindowDays = preArrivalWindowDays }),
            NullLogger<ReservationLifecycleEventGenerator>.Instance);
    }

    private static ReservationGraph SeedGraph(
        ApplicationDbContext dbContext,
        string timeZone = "Africa/Nairobi",
        DateOnly? checkInDate = null,
        DateOnly? checkOutDate = null,
        ReservationStatus status = ReservationStatus.Confirmed)
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var company = NewCompany(companyId);
        var property = NewProperty(companyId, propertyId, timeZone);
        var guest = NewGuest(companyId, guestId);
        var reservation = NewReservation(companyId, propertyId, guestId, checkInDate ?? new DateOnly(2026, 8, 10), checkOutDate ?? new DateOnly(2026, 8, 14), status: status);

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
        return new Company { Id = companyId, Name = $"Company {suffix}", Slug = $"company-{suffix}", NormalizedSlug = $"COMPANY-{suffix}".ToUpperInvariant(), Status = "Active", Email = $"{suffix}@example.com", PhoneNumber = "+254700000001", CountryCode = "KE", TimeZone = "Africa/Nairobi", IsActive = true };
    }

    private static Property NewProperty(Guid companyId, Guid propertyId, string timeZone)
    {
        return new Property { Id = propertyId, CompanyId = companyId, Name = "Demo Property", AddressLine1 = "Road", City = "Nairobi", CountryCode = "KE", TimeZone = timeZone, IsActive = true };
    }

    private static Guest NewGuest(Guid companyId, Guid guestId)
    {
        return new Guest { Id = guestId, CompanyId = companyId, FirstName = "Ada", LastName = "Guest", PreferredLanguage = "en", CountryCode = "KE", IsActive = true };
    }

    private static Reservation NewReservation(Guid companyId, Guid propertyId, Guid guestId, DateOnly checkInDate, DateOnly checkOutDate, Guid? reservationId = null, ReservationStatus status = ReservationStatus.Confirmed)
    {
        return new Reservation { Id = reservationId ?? Guid.NewGuid(), CompanyId = companyId, PropertyId = propertyId, PrimaryGuestId = guestId, ReservationSource = "Manual", CheckInDate = checkInDate, CheckOutDate = checkOutDate, Adults = 2, Children = 0, TotalGuestCount = 2, Status = status, IsActive = true };
    }

    private sealed record ReservationGraph(Guid CompanyId, Company Company, Property Property, Guest Guest, Reservation Reservation);
}

public sealed class ReservationLifecycleEventProcessorTests
{
    [Fact]
    public async Task ProcessDueAsync_HandlesBatchAndFailureDoesNotBlockOtherEvents()
    {
        var companyId = Guid.NewGuid();
        var successful = NewLifecycleEvent(companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var failing = NewLifecycleEvent(companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var repository = new ProcessorFakeRepository([successful, failing]);
        repository.Reservations[successful.Id] = NewReservation(successful);
        repository.Reservations[failing.Id] = NewReservation(failing);
        var handler = new RecordingHandler(eventToFail: failing.Id);
        var processor = CreateProcessor(repository, handler, new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));

        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(2, result.Claimed);
        Assert.Equal(1, result.Processed);
        Assert.Equal(1, result.Failed);
        Assert.Equal(2, handler.HandledEventIds.Count);
        Assert.Equal(ReservationLifecycleEventStatus.Processed, successful.Status);
        Assert.Equal(ReservationLifecycleEventStatus.Failed, failing.Status);
        Assert.Equal("handler failure", failing.LastError);
    }

    [Fact]
    public async Task ProcessDueAsync_CancellationIsNotPersistedAsFailure()
    {
        var lifecycleEvent = NewLifecycleEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var repository = new ProcessorFakeRepository([lifecycleEvent]);
        repository.Reservations[lifecycleEvent.Id] = NewReservation(lifecycleEvent);
        using var cancellationTokenSource = new CancellationTokenSource();
        var handler = new RecordingHandler(cancelTokenSource: cancellationTokenSource);
        var processor = CreateProcessor(repository, handler, new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.ProcessDueAsync(cancellationTokenSource.Token));

        Assert.Equal(ReservationLifecycleEventStatus.Processing, lifecycleEvent.Status);
        Assert.Null(lifecycleEvent.LastError);
    }

    [Theory]
    [InlineData(nameof(ReservationStatus.Cancelled))]
    [InlineData(nameof(ReservationStatus.NoShow))]
    public async Task ProcessDueAsync_SuppressesCancelledOrNoShowReservationsBeforeHandler(string statusName)
    {
        var lifecycleEvent = NewLifecycleEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var repository = new ProcessorFakeRepository([lifecycleEvent]);
        repository.Reservations[lifecycleEvent.Id] = NewReservation(lifecycleEvent, Enum.Parse<ReservationStatus>(statusName));
        var handler = new RecordingHandler();
        var processor = CreateProcessor(repository, handler, new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero));

        var result = await processor.ProcessDueAsync(CancellationToken.None);

        Assert.Equal(1, result.Suppressed);
        Assert.Empty(handler.HandledEventIds);
        Assert.Equal(ReservationLifecycleEventStatus.Suppressed, lifecycleEvent.Status);
    }

    [Fact]
    public async Task ProcessDueAsync_NoOpHandlerCreatesNoConversationMessages()
    {
        await using var dbContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"noop-{Guid.NewGuid():N}").Options);
        var companyId = Guid.NewGuid();
        var lifecycleEvent = NewLifecycleEvent(companyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        dbContext.ReservationLifecycleEvents.Add(lifecycleEvent);
        await dbContext.SaveChangesAsync();

        await new NoOpReservationLifecycleEventHandler(NullLogger<NoOpReservationLifecycleEventHandler>.Instance).HandleAsync(lifecycleEvent, CancellationToken.None);

        Assert.Empty(dbContext.ConversationMessages);
    }

    private static ReservationLifecycleEventProcessor CreateProcessor(ProcessorFakeRepository repository, IReservationLifecycleEventHandler handler, DateTimeOffset utcNow)
    {
        var timeProvider = new MutableTimeProvider(utcNow);
        return new ReservationLifecycleEventProcessor(
            repository,
            new FakeLifecycleEventService(repository, timeProvider),
            handler,
            new ReservationLifecycleEventIdempotencyKeyBuilder(),
            timeProvider,
            Options.Create(new ReservationLifecycleEventOptions { ProcessingBatchSize = 10 }),
            Options.Create(new ReservationContextOptions { PreArrivalWindowDays = 7 }),
            NullLogger<ReservationLifecycleEventProcessor>.Instance);
    }

    private static ReservationLifecycleEvent NewLifecycleEvent(Guid companyId, Guid reservationId, Guid propertyId, Guid guestId)
    {
        var propertyLocalDate = new DateOnly(2026, 8, 10);
        return new ReservationLifecycleEvent { Id = Guid.NewGuid(), CompanyId = companyId, ReservationId = reservationId, PropertyId = propertyId, GuestId = guestId, EventType = ReservationLifecycleEventType.ArrivalDay, RuleVersion = ReservationLifecycleRuleVersions.V1, PropertyLocalDate = propertyLocalDate, ScheduledForUtc = new DateTimeOffset(2026, 8, 10, 6, 0, 0, TimeSpan.Zero), Status = ReservationLifecycleEventStatus.Pending, IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(companyId, reservationId, ReservationLifecycleEventType.ArrivalDay, propertyLocalDate, ReservationLifecycleRuleVersions.V1) };
    }

    private static Reservation NewReservation(ReservationLifecycleEvent lifecycleEvent, ReservationStatus status = ReservationStatus.Confirmed)
    {
        return new Reservation { Id = lifecycleEvent.ReservationId, CompanyId = lifecycleEvent.CompanyId, PropertyId = lifecycleEvent.PropertyId, PrimaryGuestId = lifecycleEvent.GuestId, ReservationSource = "Manual", CheckInDate = lifecycleEvent.PropertyLocalDate, CheckOutDate = lifecycleEvent.PropertyLocalDate.AddDays(3), Adults = 1, TotalGuestCount = 1, Status = status, IsActive = true };
    }
}

public sealed class ReservationLifecycleWorkerTests
{
    [Fact]
    public async Task DisabledWorker_PerformsNoWork()
    {
        var generator = new WorkerGeneratorStub();
        var processor = new WorkerProcessorStub();
        await using var provider = BuildWorkerProvider(generator, processor);
        var worker = new ReservationLifecycleWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new ReservationLifecycleEventOptions { WorkerEnabled = false }), NullLogger<ReservationLifecycleWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        Assert.Equal(0, generator.Calls);
        Assert.Equal(0, processor.Calls);
    }

    [Fact]
    public async Task EnabledWorker_ResolvesGenerationAndProcessingAndStopsCleanly()
    {
        var generator = new WorkerGeneratorStub();
        var processor = new WorkerProcessorStub();
        await using var provider = BuildWorkerProvider(generator, processor);
        var worker = new ReservationLifecycleWorker(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new ReservationLifecycleEventOptions { WorkerEnabled = true, PollingIntervalSeconds = 60 }), NullLogger<ReservationLifecycleWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await processor.Called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(generator.Calls >= 1);
        Assert.True(processor.Calls >= 1);
    }

    private static Microsoft.Extensions.DependencyInjection.ServiceProvider BuildWorkerProvider(IReservationLifecycleEventGenerator generator, IReservationLifecycleEventProcessor processor)
    {
        var services = new ServiceCollection();
        services.AddSingleton(generator);
        services.AddSingleton(processor);
        return services.BuildServiceProvider();
    }
}

public sealed class ReservationLifecycleEventPostgresClaimTests : IAsyncLifetime
{
    private const string TestPostgresConnectionVariable = "STAYFLOW_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionVariable = "ConnectionStrings__DefaultConnection";
    private const string DefaultMaintenanceConnection = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    private readonly string databaseName = $"stayflow_lifecycle_claim_{Guid.NewGuid():N}";
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
        await using (var terminateCommand = new NpgsqlCommand($"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{databaseName}'", maintenanceConnection))
        {
            await terminateCommand.ExecuteNonQueryAsync();
        }

        await using var dropCommand = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", maintenanceConnection);
        await dropCommand.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task ConcurrentGeneration_CreatesExactlyOneLogicalEventSet()
    {
        await SeedGraphAsync();
        var nowUtc = new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);

        var first = GenerateWithNewContextAsync(nowUtc);
        var second = GenerateWithNewContextAsync(nowUtc);

        var results = await Task.WhenAll(first, second);

        Assert.Equal(5, results.Sum());
        await using var verificationContext = new ApplicationDbContext(dbOptions);
        Assert.Equal(5, await verificationContext.ReservationLifecycleEvents.CountAsync());
    }

    [Fact]
    public async Task ClaimDueAsync_ClaimsOnlyDuePendingEventsAndHonorsBatchSize()
    {
        var graph = await SeedGraphAsync();
        var nowUtc = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        await InsertEventAsync(graph, ReservationLifecycleEventType.PreArrival, nowUtc.AddMinutes(-1));
        await InsertEventAsync(graph, ReservationLifecycleEventType.ArrivalDay, nowUtc.AddMinutes(-2));
        await InsertEventAsync(graph, ReservationLifecycleEventType.CheckoutDay, nowUtc.AddMinutes(10));

        await using var dbContext = new ApplicationDbContext(dbOptions);
        var repository = new ReservationLifecycleEventRepository(dbContext);
        var claimed = await repository.ClaimDueAsync(nowUtc, 1, CancellationToken.None);

        var lifecycleEvent = Assert.Single(claimed);
        Assert.Equal(ReservationLifecycleEventStatus.Processing, lifecycleEvent.Status);
        Assert.Equal(1, lifecycleEvent.AttemptCount);
        Assert.Equal(nowUtc, lifecycleEvent.LastAttemptAtUtc);
        Assert.Equal(2, await dbContext.ReservationLifecycleEvents.CountAsync(item => item.Status == ReservationLifecycleEventStatus.Pending));
    }

    [Fact]
    public async Task ConcurrentClaimDueAsync_ReturnsEachEventToAtMostOneClaimer()
    {
        var graph = await SeedGraphAsync();
        var nowUtc = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 6; i++)
        {
            await InsertEventAsync(graph, (ReservationLifecycleEventType)(i % 5), nowUtc.AddMinutes(-i - 1), propertyLocalDate: new DateOnly(2026, 8, 10).AddDays(i));
        }

        var first = ClaimWithNewContextAsync(nowUtc, 6);
        var second = ClaimWithNewContextAsync(nowUtc, 6);
        var results = (await Task.WhenAll(first, second)).SelectMany(item => item).ToList();

        Assert.Equal(results.Count, results.Select(item => item.Id).Distinct().Count());
        Assert.Equal(6, results.Count);
    }

    [Fact]
    public async Task Recovery_RestoresStaleProcessingAndRetryableFailedButHonorsFreshAndMaxAttempts()
    {
        var graph = await SeedGraphAsync();
        var nowUtc = new DateTimeOffset(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
        var stale = await InsertEventAsync(graph, ReservationLifecycleEventType.PreArrival, nowUtc.AddMinutes(-30), ReservationLifecycleEventStatus.Processing, 1, nowUtc.AddMinutes(-30));
        var fresh = await InsertEventAsync(graph, ReservationLifecycleEventType.ArrivalDay, nowUtc.AddMinutes(-1), ReservationLifecycleEventStatus.Processing, 1, nowUtc.AddMinutes(-2));
        var retryable = await InsertEventAsync(graph, ReservationLifecycleEventType.CheckoutDay, nowUtc.AddMinutes(-1), ReservationLifecycleEventStatus.Failed, 1, nowUtc.AddMinutes(-10));
        var maxed = await InsertEventAsync(graph, ReservationLifecycleEventType.PostStay, nowUtc.AddMinutes(-1), ReservationLifecycleEventStatus.Failed, 3, nowUtc.AddMinutes(-10));
        var beforeRetryDelay = await InsertEventAsync(graph, ReservationLifecycleEventType.InStay, nowUtc.AddMinutes(-1), ReservationLifecycleEventStatus.Failed, 1, nowUtc.AddMinutes(-2), new DateOnly(2026, 8, 30));

        await using var dbContext = new ApplicationDbContext(dbOptions);
        var repository = new ReservationLifecycleEventRepository(dbContext);
        Assert.Equal(1, await repository.RecoverStaleProcessingAsync(nowUtc.AddMinutes(-15), nowUtc, CancellationToken.None));
        Assert.Equal(1, await repository.RecoverRetryableFailedAsync(nowUtc.AddMinutes(-5), nowUtc, 3, CancellationToken.None));
        var claimed = await repository.ClaimDueAsync(nowUtc, 10, CancellationToken.None);

        Assert.Contains(claimed, item => item.Id == stale);
        Assert.Contains(claimed, item => item.Id == retryable);
        Assert.DoesNotContain(claimed, item => item.Id == fresh || item.Id == maxed || item.Id == beforeRetryDelay);
        Assert.All(claimed, item => Assert.True(item.AttemptCount >= 2));
    }

    private async Task<int> GenerateWithNewContextAsync(DateTimeOffset nowUtc)
    {
        await using var dbContext = new ApplicationDbContext(dbOptions);
        var repository = new ReservationLifecycleEventRepository(dbContext);
        var timeProvider = new MutableTimeProvider(nowUtc);
        var generator = new ReservationLifecycleEventGenerator(
            repository,
            new ReservationLifecycleEventService(repository, new ReservationLifecycleEventIdempotencyKeyBuilder(), timeProvider, Options.Create(new ReservationLifecycleEventOptions())),
            new ReservationLifecycleEventIdempotencyKeyBuilder(),
            timeProvider,
            Options.Create(new ReservationLifecycleEventOptions()),
            Options.Create(new ReservationContextOptions { PreArrivalWindowDays = 7 }),
            NullLogger<ReservationLifecycleEventGenerator>.Instance);

        return await generator.GenerateAsync(CancellationToken.None);
    }

    private async Task<IReadOnlyCollection<ReservationLifecycleEvent>> ClaimWithNewContextAsync(DateTimeOffset nowUtc, int batchSize)
    {
        await using var dbContext = new ApplicationDbContext(dbOptions);
        return await new ReservationLifecycleEventRepository(dbContext).ClaimDueAsync(nowUtc, batchSize, CancellationToken.None);
    }

    private async Task<Guid> InsertEventAsync(ReservationGraph graph, ReservationLifecycleEventType eventType, DateTimeOffset scheduledForUtc, ReservationLifecycleEventStatus status = ReservationLifecycleEventStatus.Pending, int attemptCount = 0, DateTimeOffset? lastAttemptAtUtc = null, DateOnly? propertyLocalDate = null)
    {
        var localDate = propertyLocalDate ?? new DateOnly(2026, 8, 10).AddDays((int)eventType);
        var lifecycleEvent = new ReservationLifecycleEvent { Id = Guid.NewGuid(), CompanyId = graph.CompanyId, ReservationId = graph.Reservation.Id, PropertyId = graph.Property.Id, GuestId = graph.Guest.Id, EventType = eventType, RuleVersion = ReservationLifecycleRuleVersions.V1, PropertyLocalDate = localDate, ScheduledForUtc = scheduledForUtc, Status = status, AttemptCount = attemptCount, LastAttemptAtUtc = lastAttemptAtUtc, IdempotencyKey = new ReservationLifecycleEventIdempotencyKeyBuilder().Build(graph.CompanyId, graph.Reservation.Id, eventType, localDate, ReservationLifecycleRuleVersions.V1) };
        await using var dbContext = new ApplicationDbContext(dbOptions);
        dbContext.ReservationLifecycleEvents.Add(lifecycleEvent);
        await dbContext.SaveChangesAsync();
        return lifecycleEvent.Id;
    }

    private async Task<ReservationGraph> SeedGraphAsync()
    {
        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var company = new Company { Id = companyId, Name = "Company", Slug = companyId.ToString("N"), NormalizedSlug = companyId.ToString("N").ToUpperInvariant(), Status = "Active", Email = "company@example.com", PhoneNumber = "+254700000001", CountryCode = "KE", TimeZone = "Africa/Nairobi", IsActive = true };
        var property = new Property { Id = propertyId, CompanyId = companyId, Name = "Property", AddressLine1 = "Road", City = "Nairobi", CountryCode = "KE", TimeZone = "Africa/Nairobi", IsActive = true };
        var guest = new Guest { Id = guestId, CompanyId = companyId, FirstName = "Ada", LastName = "Guest", PreferredLanguage = "en", CountryCode = "KE", IsActive = true };
        var reservation = new Reservation { Id = Guid.NewGuid(), CompanyId = companyId, PropertyId = propertyId, PrimaryGuestId = guestId, ReservationSource = "Manual", CheckInDate = new DateOnly(2026, 8, 10), CheckOutDate = new DateOnly(2026, 8, 14), Adults = 1, TotalGuestCount = 1, Status = ReservationStatus.Confirmed, IsActive = true };
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

internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

internal sealed class RecordingHandler(Guid? eventToFail = null, CancellationTokenSource? cancelTokenSource = null) : IReservationLifecycleEventHandler
{
    public List<Guid> HandledEventIds { get; } = [];

    public Task HandleAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken)
    {
        HandledEventIds.Add(lifecycleEvent.Id);
        cancelTokenSource?.Cancel();
        cancellationToken.ThrowIfCancellationRequested();

        if (eventToFail == lifecycleEvent.Id)
        {
            throw new InvalidOperationException("handler failure");
        }

        return Task.CompletedTask;
    }
}

internal sealed class ProcessorFakeRepository(IReadOnlyCollection<ReservationLifecycleEvent> events) : IReservationLifecycleEventRepository
{
    public Dictionary<Guid, Reservation> Reservations { get; } = [];

    public Task<IReadOnlyCollection<ReservationLifecycleEvent>> ClaimDueAsync(DateTimeOffset nowUtc, int batchSize, CancellationToken cancellationToken)
    {
        var claimed = events.Where(item => item.Status == ReservationLifecycleEventStatus.Pending && item.ScheduledForUtc <= nowUtc).Take(batchSize).ToList();
        foreach (var lifecycleEvent in claimed)
        {
            lifecycleEvent.Status = ReservationLifecycleEventStatus.Processing;
            lifecycleEvent.AttemptCount++;
            lifecycleEvent.LastAttemptAtUtc = nowUtc;
        }

        return Task.FromResult<IReadOnlyCollection<ReservationLifecycleEvent>>(claimed);
    }

    public Task<ReservationLifecycleEvent?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(events.FirstOrDefault(item => item.CompanyId == companyId && item.Id == id));

    public Task<Reservation?> GetReservationForEventAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken) =>
        Task.FromResult(Reservations.GetValueOrDefault(lifecycleEvent.Id));

    public Task<int> RecoverStaleProcessingAsync(DateTimeOffset staleBeforeUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<int> RecoverRetryableFailedAsync(DateTimeOffset retryBeforeUtc, DateTimeOffset nowUtc, int maxAttempts, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task<ReservationLifecycleEvent?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(events.FirstOrDefault(item => item.CompanyId == companyId && item.IdempotencyKey == idempotencyKey));
    public Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<ReservationLifecycleEvent>>([]);
    public Task<IReadOnlyCollection<Reservation>> GetGenerationCandidatesAsync(DateOnly windowStart, DateOnly windowEnd, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Reservation>>([]);
    public Task<int> SuppressObsoleteUnprocessedAsync(Guid companyId, Guid reservationId, IReadOnlyCollection<string> currentIdempotencyKeys, DateTimeOffset nowUtc, string reason, CancellationToken cancellationToken) => Task.FromResult(0);
    public Task AddAsync(ReservationLifecycleEvent lifecycleEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    public void Detach(ReservationLifecycleEvent lifecycleEvent) { }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FakeLifecycleEventService(ProcessorFakeRepository repository, TimeProvider timeProvider) : IReservationLifecycleEventService
{
    public Task<ReservationLifecycleEventCreationResult> TryCreateAsync(Reservation reservation, Property property, ReservationLifecycleEventType eventType, DateOnly propertyLocalDate, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ReservationLifecycleEvent?> GetAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken) => repository.GetByIdAsync(companyId, eventId, cancellationToken);
    public Task<IReadOnlyCollection<ReservationLifecycleEvent>> GetPendingAsync(Guid companyId, DateTimeOffset dueBeforeUtc, int limit, CancellationToken cancellationToken) => repository.GetPendingAsync(companyId, dueBeforeUtc, limit, cancellationToken);

    public async Task<ReservationLifecycleEvent> MarkProcessingAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken) => await GetRequiredAsync(companyId, eventId, cancellationToken);

    public async Task<ReservationLifecycleEvent> MarkProcessedAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken)
    {
        var lifecycleEvent = await GetRequiredAsync(companyId, eventId, cancellationToken);
        lifecycleEvent.Status = ReservationLifecycleEventStatus.Processed;
        lifecycleEvent.ProcessedAtUtc = timeProvider.GetUtcNow();
        lifecycleEvent.LastError = null;
        return lifecycleEvent;
    }

    public async Task<ReservationLifecycleEvent> MarkFailedAsync(Guid companyId, Guid eventId, string error, CancellationToken cancellationToken)
    {
        var lifecycleEvent = await GetRequiredAsync(companyId, eventId, cancellationToken);
        lifecycleEvent.Status = ReservationLifecycleEventStatus.Failed;
        lifecycleEvent.LastAttemptAtUtc = timeProvider.GetUtcNow();
        lifecycleEvent.LastError = error;
        return lifecycleEvent;
    }

    public async Task<ReservationLifecycleEvent> MarkSuppressedAsync(Guid companyId, Guid eventId, string reason, CancellationToken cancellationToken)
    {
        var lifecycleEvent = await GetRequiredAsync(companyId, eventId, cancellationToken);
        lifecycleEvent.Status = ReservationLifecycleEventStatus.Suppressed;
        lifecycleEvent.ProcessedAtUtc = timeProvider.GetUtcNow();
        lifecycleEvent.LastError = reason;
        return lifecycleEvent;
    }

    private async Task<ReservationLifecycleEvent> GetRequiredAsync(Guid companyId, Guid eventId, CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(companyId, eventId, cancellationToken) ?? throw new KeyNotFoundException();
}

internal sealed class WorkerGeneratorStub : IReservationLifecycleEventGenerator
{
    public int Calls { get; private set; }
    public Task<int> GenerateAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(0);
    }
}

internal sealed class WorkerProcessorStub : IReservationLifecycleEventProcessor
{
    public int Calls { get; private set; }
    public TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ReservationLifecycleEventProcessingResult> ProcessDueAsync(CancellationToken cancellationToken)
    {
        Calls++;
        Called.TrySetResult();
        return Task.FromResult(new ReservationLifecycleEventProcessingResult(0, 0, 0, 0, 0, 0));
    }
}