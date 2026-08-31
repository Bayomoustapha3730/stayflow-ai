using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ConciergeActions;
using StayFlow.Api.DTOs.Payments;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Services;
using StayFlow.Api.Services.AI.Memory;
using StayFlow.Api.Services.ConciergeActions;
using StayFlow.Api.Services.Payments;

namespace StayFlow.Api.Tests;

/// <summary>
/// Regression coverage for the production PostgreSQL 23505 unique-constraint failure on
/// IX_PendingConciergeActions_IdempotencyKey. Runs against a real, ephemeral Npgsql/PostgreSQL
/// database (created and dropped per test) rather than the EF InMemory provider, because
/// InMemory does not enforce unique indexes and would silently hide this class of bug.
/// Requires a reachable PostgreSQL server. Connection resolution order:
/// STAYFLOW_TEST_POSTGRES_CONNECTION; ConnectionStrings__DefaultConnection with Database=postgres;
/// finally a local development fallback.
/// </summary>
public sealed class ConciergeActionOrchestratorPaymentRetryTests : IAsyncLifetime
{
    private const string TestPostgresConnectionVariable = "STAYFLOW_TEST_POSTGRES_CONNECTION";
    private const string DefaultConnectionVariable = "ConnectionStrings__DefaultConnection";
    private const string DefaultMaintenanceConnection = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

    private readonly string databaseName = $"stayflow_test_{Guid.NewGuid():N}";
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

    [Theory]
    [InlineData(nameof(PaymentStatus.Failed))]
    [InlineData(nameof(PaymentStatus.Cancelled))]
    [InlineData(nameof(PaymentStatus.Expired))]
    public async Task HandleGuestMessageAsync_AfterHistoricalPaymentActionWithTerminalPayment_CreatesNewActionWithDistinctKey(string terminalPaymentStatus)
    {
        await using var seedContext = new ApplicationDbContext(dbOptions);

        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PhoneNumber = "+254700000099",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };

        seedContext.Companies.Add(NewCompany(companyId));
        seedContext.Properties.Add(NewProperty(propertyId, companyId));
        seedContext.Guests.Add(guest);
        seedContext.Reservations.Add(new Reservation
        {
            Id = reservationId,
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guest.Id,
            CheckInDate = new DateOnly(2026, 9, 1),
            CheckOutDate = new DateOnly(2026, 9, 5),
            ReservationSource = "Manual",
            Status = ReservationStatus.Confirmed,
            Currency = "KES",
            BookingAmount = 3000m,
            IsActive = true
        });

        seedContext.Conversations.Add(new Conversation
        {
            Id = conversationId,
            CompanyId = companyId,
            GuestId = guest.Id,
            PropertyId = propertyId,
            ReservationId = reservationId,
            Channel = GuestChannel.Web,
            Status = ConversationStatus.Open,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow
        });

        // Historical terminal M-PESA payment attempt (the guest's earlier failed/cancelled/expired try).
        seedContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            GuestId = guest.Id,
            ReservationId = reservationId,
            Amount = 3000m,
            Currency = "KES",
            Provider = "M-PESA",
            Status = terminalPaymentStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        // Historical RequestPayment PendingConciergeAction using the normal/base idempotency key.
        var paymentAction = new PaymentRequestAction(conversationId, reservationId, propertyId, guest.PhoneNumber!, 3000m, "KES", null);
        var serialized = ConciergeActionSerialization.Serialize(paymentAction);
        var idempotencyService = new ConciergeActionIdempotencyService();
        var baseKey = idempotencyService.CreateKey(companyId, conversationId, ConciergeActionType.RequestPayment, propertyId, reservationId, serialized);

        var historicalAction = new PendingConciergeAction
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversationId,
            PropertyId = propertyId,
            ReservationId = reservationId,
            ActionType = ConciergeActionType.RequestPayment,
            SerializedNormalizedParameters = serialized,
            Status = PendingConciergeActionStatus.Completed,
            IdempotencyKey = baseKey,
            CreatedFromMessageId = Guid.NewGuid(),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-25)
        };
        seedContext.PendingConciergeActions.Add(historicalAction);

        await seedContext.SaveChangesAsync();

        // Sanity check: proves the historical row occupies the exact key a fresh (unsuffixed)
        // request would collide with. Inserting a second row with the same key must fail with
        // a unique-constraint violation, exactly matching the diagnosed production 23505 failure.
        await using (var collisionContext = new ApplicationDbContext(dbOptions))
        {
            collisionContext.PendingConciergeActions.Add(new PendingConciergeAction
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ConversationId = conversationId,
                PropertyId = propertyId,
                ReservationId = reservationId,
                ActionType = ConciergeActionType.RequestPayment,
                SerializedNormalizedParameters = serialized,
                Status = PendingConciergeActionStatus.ReadyToExecute,
                IdempotencyKey = baseKey,
                CreatedFromMessageId = Guid.NewGuid(),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => collisionContext.SaveChangesAsync());
        }

        // Now exercise the real orchestrator, which must avoid that collision.
        await using var dbContext = new ApplicationDbContext(dbOptions);
        var conversation = new Conversation
        {
            Id = conversationId,
            CompanyId = companyId,
            GuestId = guest.Id,
            PropertyId = propertyId,
            ReservationId = reservationId,
            Channel = GuestChannel.Web,
            Status = ConversationStatus.Open,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            Guest = guest
        };

        var executor = new FakeExecutor();
        var orchestrator = new ConciergeActionOrchestrator(
            dbContext,
            new FakeDetector(paymentAction),
            new FakePolicy(),
            executor,
            new FakeAuditService(),
            new FakeConfirmationService(),
            idempotencyService,
            new FakeFormatter(),
            new FakeMemoryService(),
            new FakePaymentGroundingService(3000m, "KES"),
            Options.Create(new ConciergeActionsOptions()),
            new FakeTenantContext(companyId));

        var newGuestMessageId = Guid.NewGuid();
        var result = await orchestrator.HandleGuestMessageAsync(companyId, conversation, newGuestMessageId, "Please send me the payment link again", CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal(1, executor.CallCount);

        var actions = await dbContext.PendingConciergeActions
            .Where(item => item.CompanyId == companyId && item.ActionType == ConciergeActionType.RequestPayment)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, actions.Count);

        var reloadedHistorical = actions.Single(item => item.Id == historicalAction.Id);
        var newAction = actions.Single(item => item.Id != historicalAction.Id);

        // Historical action/key is untouched.
        Assert.Equal(baseKey, reloadedHistorical.IdempotencyKey);
        Assert.Equal(PendingConciergeActionStatus.Completed, reloadedHistorical.Status);

        // New action has a distinct key derived from the base key + the new guest message id.
        Assert.NotEqual(baseKey, newAction.IdempotencyKey);
        Assert.Equal($"{baseKey}:{newGuestMessageId:N}", newAction.IdempotencyKey);
        Assert.True(newAction.IdempotencyKey.Length <= 200, "IdempotencyKey must fit the configured 200-char column/index.");
    }

    [Fact]
    public async Task HandleGuestMessageAsync_WithActiveMpesaPayment_BlocksNewPaymentAction()
    {
        await using var seedContext = new ApplicationDbContext(dbOptions);

        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PhoneNumber = "+254700000099",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };

        seedContext.Companies.Add(NewCompany(companyId));
        seedContext.Properties.Add(NewProperty(propertyId, companyId));
        seedContext.Guests.Add(guest);
        seedContext.Reservations.Add(new Reservation
        {
            Id = reservationId,
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guest.Id,
            CheckInDate = new DateOnly(2026, 9, 1),
            CheckOutDate = new DateOnly(2026, 9, 5),
            ReservationSource = "Manual",
            Status = ReservationStatus.Confirmed,
            Currency = "KES",
            BookingAmount = 3000m,
            IsActive = true
        });
        seedContext.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            GuestId = guest.Id,
            ReservationId = reservationId,
            Amount = 3000m,
            Currency = "KES",
            Provider = "M-PESA",
            Status = PaymentStatus.Processing.ToStorageValue(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await seedContext.SaveChangesAsync();

        await using var dbContext = new ApplicationDbContext(dbOptions);
        var conversation = new Conversation
        {
            Id = conversationId,
            CompanyId = companyId,
            GuestId = guest.Id,
            PropertyId = propertyId,
            ReservationId = reservationId,
            Channel = GuestChannel.Web,
            Status = ConversationStatus.Open,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            Guest = guest
        };

        var paymentAction = new PaymentRequestAction(conversationId, reservationId, propertyId, guest.PhoneNumber!, 3000m, "KES", null);
        var executor = new FakeExecutor();
        var orchestrator = new ConciergeActionOrchestrator(
            dbContext,
            new FakeDetector(paymentAction),
            new FakePolicy(),
            executor,
            new FakeAuditService(),
            new FakeConfirmationService(),
            new ConciergeActionIdempotencyService(),
            new FakeFormatter(),
            new FakeMemoryService(),
            new FakePaymentGroundingService(3000m, "KES"),
            Options.Create(new ConciergeActionsOptions()),
            new FakeTenantContext(companyId));

        var result = await orchestrator.HandleGuestMessageAsync(companyId, conversation, Guid.NewGuid(), "Please send me a payment link", CancellationToken.None);

        Assert.True(result.Handled);
        Assert.Equal("ActivePaymentExists", result.FailureCode);
        Assert.Equal(0, executor.CallCount);
        Assert.Empty(await dbContext.PendingConciergeActions.Where(item => item.CompanyId == companyId).ToListAsync());
    }

    [Fact]
    public async Task ConfirmPendingActionAsync_CalledTwiceForSameAction_DoesNotExecuteTwice()
    {
        await using var seedContext = new ApplicationDbContext(dbOptions);

        var companyId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FirstName = "Ada",
            LastName = "Guest",
            PhoneNumber = "+254700000099",
            PreferredLanguage = "en",
            CountryCode = "KE",
            IsActive = true
        };

        seedContext.Companies.Add(NewCompany(companyId));
        seedContext.Properties.Add(NewProperty(propertyId, companyId));
        seedContext.Guests.Add(guest);
        seedContext.Reservations.Add(new Reservation
        {
            Id = reservationId,
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guest.Id,
            CheckInDate = new DateOnly(2026, 9, 1),
            CheckOutDate = new DateOnly(2026, 9, 5),
            ReservationSource = "Manual",
            Status = ReservationStatus.Confirmed,
            Currency = "KES",
            BookingAmount = 3000m,
            IsActive = true
        });

        seedContext.Conversations.Add(new Conversation
        {
            Id = conversationId,
            CompanyId = companyId,
            GuestId = guest.Id,
            PropertyId = propertyId,
            ReservationId = reservationId,
            Channel = GuestChannel.Web,
            Status = ConversationStatus.Open,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow
        });

        var paymentAction = new PaymentRequestAction(conversationId, reservationId, propertyId, guest.PhoneNumber!, 3000m, "KES", null);
        var serialized = ConciergeActionSerialization.Serialize(paymentAction);
        var idempotencyService = new ConciergeActionIdempotencyService();
        var key = idempotencyService.CreateKey(companyId, conversationId, ConciergeActionType.RequestPayment, propertyId, reservationId, serialized);

        var pendingAction = new PendingConciergeAction
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ConversationId = conversationId,
            PropertyId = propertyId,
            ReservationId = reservationId,
            ActionType = ConciergeActionType.RequestPayment,
            SerializedNormalizedParameters = serialized,
            Status = PendingConciergeActionStatus.AwaitingGuestConfirmation,
            IdempotencyKey = key,
            CreatedFromMessageId = Guid.NewGuid(),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        seedContext.PendingConciergeActions.Add(pendingAction);
        await seedContext.SaveChangesAsync();

        await using var dbContext = new ApplicationDbContext(dbOptions);
        var executor = new FakeExecutor();
        var orchestrator = new ConciergeActionOrchestrator(
            dbContext,
            new FakeDetector(paymentAction),
            new FakePolicy(),
            executor,
            new FakeAuditService(),
            new FakeConfirmationService(),
            idempotencyService,
            new FakeFormatter(),
            new FakeMemoryService(),
            new FakePaymentGroundingService(3000m, "KES"),
            Options.Create(new ConciergeActionsOptions()),
            new FakeTenantContext(companyId));

        var first = await orchestrator.ConfirmPendingActionAsync(companyId, conversationId, pendingAction.Id, CancellationToken.None);
        var second = await orchestrator.ConfirmPendingActionAsync(companyId, conversationId, pendingAction.Id, CancellationToken.None);

        Assert.True(first.Handled);
        Assert.True(second.Handled);
        Assert.Equal(1, executor.CallCount);
        Assert.True(second.ExecutionResult!.WasIdempotentReplay);
    }

    private static Company NewCompany(Guid id) => new()
    {
        Id = id,
        Name = "Test Co",
        Slug = $"test-{id:N}",
        NormalizedSlug = $"TEST-{id:N}",
        Status = "Active",
        Email = "test@example.com",
        PhoneNumber = "+254700000000",
        CountryCode = "KE",
        TimeZone = "Africa/Nairobi",
        IsActive = true
    };

    private static Property NewProperty(Guid id, Guid companyId) => new()
    {
        Id = id,
        CompanyId = companyId,
        Name = "Test Property",
        AddressLine1 = "Road",
        City = "Nairobi",
        CountryCode = "KE",
        TimeZone = "Africa/Nairobi",
        IsActive = true
    };

    private sealed class FakeDetector(PaymentRequestAction action) : IConciergeActionDetector
    {
        public ConciergeActionProposal Detect(Conversation conversation, string guestMessage, string? activeTopic, bool hasPendingAction)
            => new(ConciergeActionType.RequestPayment, ConciergeActionConfidenceLevel.High, action, [], false, null, true, "PaymentRequestDetected");
    }

    private sealed class FakePolicy : IConciergeActionPolicy
    {
        public (bool Allowed, string? FailureCode, string? ClarificationPrompt, ConciergeActionConfirmationRequirement ConfirmationRequirement, bool RequiresHostApproval) Validate(Conversation conversation, ConciergeActionProposal proposal)
            => (true, null, null, ConciergeActionConfirmationRequirement.None, false);
    }

    private sealed class FakeAuditService : IConciergeActionAuditService
    {
        public Task WriteAsync(Guid companyId, Guid conversationId, Guid? pendingActionId, ConciergeActionType actionType, ConciergeActionAuditEventType eventType, string actorType, Guid? actorUserId, string channel, string resultCode, string correlationId, object? metadata, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FakeConfirmationService : IConciergeActionConfirmationService
    {
        public bool IsAffirmative(string message) => true;
        public bool IsNegative(string message) => false;
        public bool IsCancel(string message) => false;
    }

    private sealed class FakeFormatter : IConciergeActionResultFormatter
    {
        public string ToGuestMessage(ConciergeActionExecutionResult result) => "ok";
    }

    private sealed class FakeMemoryService : IConversationMemoryService
    {
        public ConversationMemoryContext BuildContext(Services.AI.Context.ConversationContext context, int recentMessageCount, int characterBudget, IReadOnlyCollection<string>? priorSelectedArticleIds = null, string? pendingClarification = null)
            => new([], [], null, null, [], null, null, new Dictionary<string, string>(), string.Empty, false, DateTimeOffset.UtcNow);
    }

    private sealed class FakePaymentGroundingService(decimal remainingBalance, string currency) : IReservationPaymentGroundingService
    {
        public Task<ReservationPaymentGroundingDto?> GetReservationPaymentGroundingAsync(Guid reservationId, Guid companyId, CancellationToken cancellationToken)
            => Task.FromResult<ReservationPaymentGroundingDto?>(new ReservationPaymentGroundingDto
            {
                ReservationId = reservationId,
                BookingAmount = remainingBalance,
                Currency = currency,
                TotalPaid = 0m,
                RemainingBalance = remainingBalance,
                HasSuccessfulPayment = false
            });
    }

    private sealed class FakeExecutor : IConciergeActionExecutor
    {
        public int CallCount { get; private set; }

        public Task<ConciergeActionExecutionResult> ExecuteAsync(PendingConciergeAction pendingAction, CancellationToken cancellationToken)
        {
            if (pendingAction.Status == PendingConciergeActionStatus.Completed)
            {
                return Task.FromResult(new ConciergeActionExecutionResult(
                    pendingAction.Id, pendingAction.ActionType, pendingAction.Status, false, true, null, false, false, ConciergeActionResponseCodes.AlreadySubmitted, null, pendingAction.ExecutedAt));
            }

            CallCount++;
            pendingAction.Status = PendingConciergeActionStatus.Completed;
            pendingAction.ExecutedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(new ConciergeActionExecutionResult(
                pendingAction.Id, pendingAction.ActionType, pendingAction.Status, true, false, Guid.NewGuid(), false, false, ConciergeActionResponseCodes.PaymentRequestSubmitted, null, pendingAction.ExecutedAt));
        }
    }

    private sealed class FakeTenantContext(Guid companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "test";
        public bool IsAuthenticated { get; } = true;
    }
}
