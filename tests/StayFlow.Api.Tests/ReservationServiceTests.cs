using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Reservations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationServiceTests
{
    [Fact]
    public void ReservationRequestDtos_DoNotExposeCompanyIdTenantSelector()
    {
        Assert.Null(typeof(CreateReservationRequest).GetProperty("CompanyId"));
        Assert.Null(typeof(UpdateReservationRequest).GetProperty("CompanyId"));
        Assert.Null(typeof(ReservationQueryParameters).GetProperty("CompanyId"));
        Assert.Null(typeof(CreateReservationRequest).GetProperty("Status"));
        Assert.Null(typeof(UpdateReservationRequest).GetProperty("Status"));
    }

    [Fact]
    public async Task CreateAsync_WithTenantAssociations_CreatesReservation()
    {
        var repository = new FakeReservationRepository();
        var service = CreateService(repository);

        var response = await service.CreateAsync(ValidCreateRequest(repository.PropertyId, repository.GuestId), CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Reservation created successfully.", response.Message);
        var reservation = Assert.Single(repository.Reservations);
        Assert.Equal(repository.CompanyId, reservation.CompanyId);
        Assert.Equal(repository.PropertyId, reservation.PropertyId);
        Assert.Equal(repository.GuestId, reservation.PrimaryGuestId);
        Assert.Equal(ReservationStatus.Draft, reservation.Status);
        Assert.Equal(3, reservation.TotalGuestCount);
        Assert.Equal("KES", reservation.Currency);
        Assert.Single(repository.AuditLogs);
    }

    [Fact]
    public async Task UpdateAsync_WithTenantAssociations_UpdatesReservation()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.UpdateAsync(reservation.Id, new UpdateReservationRequest
        {
            PropertyId = repository.PropertyId,
            PrimaryGuestId = repository.GuestId,
            ExternalReservationReference = " ext-002 ",
            ReservationSource = "Direct Booking",
            ConfirmationNumber = " conf-002 ",
            CheckInDate = new DateOnly(2026, 8, 10),
            CheckOutDate = new DateOnly(2026, 8, 12),
            Adults = 1,
            Children = 0,
            Currency = "usd",
            BookingAmount = 250,
            SpecialRequests = "Ground floor",
            InternalNotes = "VIP",
            IsActive = false
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("ext-002", reservation.ExternalReservationReference);
        Assert.Equal("conf-002", reservation.ConfirmationNumber);
        Assert.Equal(1, reservation.TotalGuestCount);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal("USD", reservation.Currency);
        Assert.False(reservation.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_WithTenantReservation_ReturnsReservation()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.GetByIdAsync(reservation.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(reservation.Id, response.Data.Id);
    }

    [Fact]
    public async Task GetAsync_PaginatesTenantReservations()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, confirmationNumber: "A"));
        repository.Reservations.Add(NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, confirmationNumber: "B"));
        repository.Reservations.Add(NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, confirmationNumber: "C"));
        var service = CreateService(repository);

        var response = await service.GetAsync(new ReservationQueryParameters { PageNumber = 1, PageSize = 2 }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(3, response.Data.TotalCount);
        Assert.Equal(2, response.Data.Items.Count);
    }

    [Fact]
    public async Task GetAsync_SearchesConfirmationAndReference()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, confirmationNumber: "CONF-001", externalReference: "AIR-001"));
        repository.Reservations.Add(NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, confirmationNumber: "CONF-002", externalReference: "DIRECT-002"));
        var service = CreateService(repository);

        var response = await service.GetAsync(new ReservationQueryParameters { Search = "DIRECT" }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("CONF-002", Assert.Single(response.Data!.Items).ConfirmationNumber);
    }

    [Fact]
    public async Task GetAsync_FiltersByPropertyGuestAndStatus()
    {
        var repository = new FakeReservationRepository();
        var otherPropertyId = Guid.NewGuid();
        repository.TenantPropertyIds.Add(otherPropertyId);
        repository.Reservations.Add(NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, status: ReservationStatus.Confirmed));
        repository.Reservations.Add(NewReservation(repository.CompanyId, otherPropertyId, repository.GuestId, status: ReservationStatus.Draft));
        var service = CreateService(repository);

        var response = await service.GetAsync(new ReservationQueryParameters
        {
            PropertyId = repository.PropertyId,
            PrimaryGuestId = repository.GuestId,
            Status = "Confirmed"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(repository.PropertyId, item.PropertyId);
        Assert.Equal("Confirmed", item.Status);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRequest_ReturnsValidationErrors()
    {
        var repository = new FakeReservationRepository();
        var service = CreateService(repository);

        var response = await service.CreateAsync(new CreateReservationRequest
        {
            PropertyId = Guid.Empty,
            PrimaryGuestId = Guid.Empty,
            ReservationSource = "",
            CheckInDate = new DateOnly(2026, 8, 10),
            CheckOutDate = new DateOnly(2026, 8, 9),
            Adults = 0,
            Children = 0,
            Currency = "US",
            BookingAmount = -1
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation validation failed.", response.Message);
        Assert.Contains("PropertyId is required.", response.Errors);
        Assert.Contains("PrimaryGuestId is required.", response.Errors);
        Assert.Contains("Check-out date must be after check-in date.", response.Errors);
        Assert.Contains("Total guest count must be greater than zero.", response.Errors);
        Assert.Contains("Currency must be a three-letter ISO currency code.", response.Errors);
        Assert.Contains("Booking amount must be zero or greater.", response.Errors);
    }

    [Fact]
    public async Task CreateAsync_WithMissingTenantContext_ReturnsFailure()
    {
        var repository = new FakeReservationRepository();
        var service = CreateService(repository, new FakeCurrentTenantContext(null, isAuthenticated: true));

        var response = await service.CreateAsync(ValidCreateRequest(repository.PropertyId, repository.GuestId), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is missing or invalid.", response.Message);
        Assert.Empty(repository.Reservations);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidTenantContext_ReturnsFailure()
    {
        var repository = new FakeReservationRepository();
        var service = CreateService(repository, new FakeCurrentTenantContext(Guid.Empty));

        var response = await service.CreateAsync(ValidCreateRequest(repository.PropertyId, repository.GuestId), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is missing or invalid.", response.Message);
        Assert.Empty(repository.Reservations);
    }

    [Fact]
    public async Task CreateAsync_WithoutAuthenticatedTenantContext_ReturnsFailure()
    {
        var repository = new FakeReservationRepository();
        var service = CreateService(repository, new FakeCurrentTenantContext(repository.CompanyId, isAuthenticated: false));

        var response = await service.CreateAsync(ValidCreateRequest(repository.PropertyId, repository.GuestId), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is required.", response.Message);
        Assert.Empty(repository.Reservations);
    }

    [Fact]
    public async Task CreateAsync_WithCrossTenantProperty_ReturnsNotFound()
    {
        var repository = new FakeReservationRepository();
        var service = CreateService(repository);

        var response = await service.CreateAsync(ValidCreateRequest(Guid.NewGuid(), repository.GuestId), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Property was not found.", response.Message);
        Assert.Empty(repository.Reservations);
    }

    [Fact]
    public async Task CreateAsync_WithCrossTenantGuest_ReturnsNotFound()
    {
        var repository = new FakeReservationRepository();
        var service = CreateService(repository);

        var response = await service.CreateAsync(ValidCreateRequest(repository.PropertyId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Guest was not found.", response.Message);
        Assert.Empty(repository.Reservations);
    }

    [Fact]
    public async Task GetByIdAsync_CrossTenantReservationReturnsNotFound()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(Guid.NewGuid(), repository.PropertyId, repository.GuestId);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.GetByIdAsync(reservation.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation was not found.", response.Message);
    }

    [Fact]
    public async Task UpdateAsync_CrossTenantReservationReturnsNotFound()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(Guid.NewGuid(), repository.PropertyId, repository.GuestId);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.UpdateAsync(reservation.Id, ValidUpdateRequest(repository.PropertyId, repository.GuestId), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation was not found.", response.Message);
    }

    [Fact]
    public async Task DeleteAsync_CrossTenantReservationReturnsNotFound()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(Guid.NewGuid(), repository.PropertyId, repository.GuestId);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.DeleteAsync(reservation.Id, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation was not found.", response.Message);
        Assert.False(reservation.IsDeleted);
    }

    [Fact]
    public async Task GetAsync_NeverReturnsAnotherTenantsReservations()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, confirmationNumber: "TENANT-A"));
        repository.Reservations.Add(NewReservation(Guid.NewGuid(), repository.PropertyId, repository.GuestId, confirmationNumber: "TENANT-B"));
        var service = CreateService(repository);

        var response = await service.GetAsync(new ReservationQueryParameters(), CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("TENANT-A", Assert.Single(response.Data!.Items).ConfirmationNumber);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesReservationAndSetsMetadata()
    {
        var repository = new FakeReservationRepository();
        var userId = Guid.NewGuid();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository, new FakeCurrentTenantContext(repository.CompanyId, userId: userId));

        var response = await service.DeleteAsync(reservation.Id, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(reservation.IsDeleted);
        Assert.NotNull(reservation.DeletedAt);
        Assert.Equal(userId, reservation.DeletedBy);
        Assert.Single(repository.AuditLogs);
    }

    [Fact]
    public async Task TransitionStatusAsync_DraftToPendingConfirmation_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.Draft, ReservationStatus.PendingConfirmation);
    }

    [Fact]
    public async Task TransitionStatusAsync_PendingConfirmationToConfirmed_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.PendingConfirmation, ReservationStatus.Confirmed);
    }

    [Fact]
    public async Task TransitionStatusAsync_ConfirmedToPreArrival_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.Confirmed, ReservationStatus.PreArrival);
    }

    [Fact]
    public async Task TransitionStatusAsync_PreArrivalToReadyForCheckIn_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.PreArrival, ReservationStatus.ReadyForCheckIn);
    }

    [Fact]
    public async Task TransitionStatusAsync_ReadyForCheckInToCheckedIn_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.ReadyForCheckIn, ReservationStatus.CheckedIn);
    }

    [Fact]
    public async Task TransitionStatusAsync_CheckedInToActiveStay_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.CheckedIn, ReservationStatus.ActiveStay);
    }

    [Fact]
    public async Task TransitionStatusAsync_ActiveStayToCheckOutPending_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.ActiveStay, ReservationStatus.CheckOutPending);
    }

    [Fact]
    public async Task TransitionStatusAsync_CheckOutPendingToCheckedOut_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.CheckOutPending, ReservationStatus.CheckedOut);
    }

    [Fact]
    public async Task TransitionStatusAsync_CheckedOutToPostStay_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.CheckedOut, ReservationStatus.PostStay);
    }

    [Fact]
    public async Task TransitionStatusAsync_PostStayToCompleted_Succeeds()
    {
        await AssertTransitionSucceedsAsync(ReservationStatus.PostStay, ReservationStatus.Completed);
    }

    [Fact]
    public async Task TransitionStatusAsync_DraftToActiveStay_Fails()
    {
        await AssertTransitionFailsAsync(ReservationStatus.Draft, ReservationStatus.ActiveStay);
    }

    [Fact]
    public async Task TransitionStatusAsync_CompletedToDraft_Fails()
    {
        await AssertTransitionFailsAsync(ReservationStatus.Completed, ReservationStatus.Draft);
    }

    [Fact]
    public async Task TransitionStatusAsync_CancelledToConfirmed_Fails()
    {
        await AssertTransitionFailsAsync(ReservationStatus.Cancelled, ReservationStatus.Confirmed);
    }

    [Fact]
    public async Task TransitionStatusAsync_NoShowToCheckedIn_Fails()
    {
        await AssertTransitionFailsAsync(ReservationStatus.NoShow, ReservationStatus.CheckedIn);
    }

    [Fact]
    public async Task TransitionStatusAsync_SameStatus_IsIdempotent()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, status: ReservationStatus.Confirmed);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.TransitionStatusAsync(reservation.Id, new TransitionReservationStatusRequest { TargetStatus = "Confirmed" }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Empty(repository.AuditLogs);
    }

    [Fact]
    public async Task TransitionStatusAsync_InvalidTargetStatus_Fails()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, status: ReservationStatus.Draft);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.TransitionStatusAsync(reservation.Id, new TransitionReservationStatusRequest { TargetStatus = "Flying" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation status transition failed.", response.Message);
        Assert.Contains("Target status is invalid.", response.Errors);
        Assert.Equal(ReservationStatus.Draft, reservation.Status);
    }

    [Fact]
    public async Task TransitionStatusAsync_CrossTenantReservationReturnsNotFound()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(Guid.NewGuid(), repository.PropertyId, repository.GuestId, status: ReservationStatus.Draft);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.TransitionStatusAsync(reservation.Id, new TransitionReservationStatusRequest { TargetStatus = "PendingConfirmation" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation was not found.", response.Message);
        Assert.Equal(ReservationStatus.Draft, reservation.Status);
    }

    [Fact]
    public async Task TransitionStatusAsync_WithMissingTenantContext_ReturnsFailure()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, status: ReservationStatus.Draft);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository, new FakeCurrentTenantContext(null, isAuthenticated: true));

        var response = await service.TransitionStatusAsync(reservation.Id, new TransitionReservationStatusRequest { TargetStatus = "PendingConfirmation" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Authenticated tenant context is missing or invalid.", response.Message);
        Assert.Equal(ReservationStatus.Draft, reservation.Status);
    }

    [Fact]
    public async Task TransitionStatusAsync_SuccessfulTransitionCreatesAuditRecord()
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, status: ReservationStatus.Draft);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository, new FakeCurrentTenantContext(repository.CompanyId, userId: Guid.NewGuid(), correlationId: "transition-correlation"));

        var response = await service.TransitionStatusAsync(reservation.Id, new TransitionReservationStatusRequest { TargetStatus = "PendingConfirmation" }, CancellationToken.None);

        Assert.True(response.Success);
        var auditLog = Assert.Single(repository.AuditLogs);
        Assert.Equal(nameof(Reservation), auditLog.EntityName);
        Assert.Equal(reservation.Id, auditLog.EntityId);
        Assert.Equal("StatusTransitioned", auditLog.Action);
        Assert.Contains("PreviousStatus", auditLog.Details);
        Assert.Contains("PendingConfirmation", auditLog.Details);
        Assert.Contains("transition-correlation", auditLog.Details);
    }

    private static CreateReservationRequest ValidCreateRequest(Guid propertyId, Guid guestId)
    {
        return new CreateReservationRequest
        {
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ExternalReservationReference = " AIR-123 ",
            ReservationSource = "Airbnb",
            ConfirmationNumber = " CONF-123 ",
            CheckInDate = new DateOnly(2026, 8, 1),
            CheckOutDate = new DateOnly(2026, 8, 4),
            Adults = 2,
            Children = 1,
            Currency = "kes",
            BookingAmount = 1200,
            SpecialRequests = "Late arrival",
            InternalNotes = "Call before check-in"
        };
    }

    private static UpdateReservationRequest ValidUpdateRequest(Guid propertyId, Guid guestId)
    {
        return new UpdateReservationRequest
        {
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ReservationSource = "Manual",
            CheckInDate = new DateOnly(2026, 8, 1),
            CheckOutDate = new DateOnly(2026, 8, 3),
            Adults = 1,
            Children = 0
        };
    }

    private static Reservation NewReservation(
        Guid companyId,
        Guid propertyId,
        Guid guestId,
        string confirmationNumber = "CONF-001",
        string externalReference = "EXT-001",
        ReservationStatus status = ReservationStatus.Confirmed,
        bool isDeleted = false)
    {
        return new Reservation
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            PrimaryGuestId = guestId,
            ExternalReservationReference = externalReference,
            ReservationSource = "Airbnb",
            ConfirmationNumber = confirmationNumber,
            CheckInDate = new DateOnly(2026, 8, 1),
            CheckOutDate = new DateOnly(2026, 8, 4),
            Adults = 2,
            Children = 0,
            TotalGuestCount = 2,
            Status = status,
            Currency = "KES",
            BookingAmount = 1000,
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

    private sealed class FakeReservationRepository : IReservationRepository
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid PropertyId { get; } = Guid.NewGuid();
        public Guid GuestId { get; } = Guid.NewGuid();
        public bool CompanyExists { get; init; } = true;
        public HashSet<Guid> TenantPropertyIds { get; } = [];
        public HashSet<Guid> TenantGuestIds { get; } = [];
        public List<Reservation> Reservations { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];

        public FakeReservationRepository()
        {
            TenantPropertyIds.Add(PropertyId);
            TenantGuestIds.Add(GuestId);
        }

        public Task<PagedResult<Reservation>> GetAsync(Guid companyId, ReservationQueryParameters query, CancellationToken cancellationToken)
        {
            var pageNumber = query.NormalizedPageNumber;
            var pageSize = query.NormalizedPageSize;
            var reservations = Reservations
                .Where(reservation => reservation.CompanyId == companyId)
                .Where(reservation => !reservation.IsDeleted)
                .Where(reservation => query.PropertyId is null || reservation.PropertyId == query.PropertyId)
                .Where(reservation => query.PrimaryGuestId is null || reservation.PrimaryGuestId == query.PrimaryGuestId)
                .Where(reservation => string.IsNullOrWhiteSpace(query.Status) || reservation.Status.ToString().Equals(query.Status, StringComparison.OrdinalIgnoreCase))
                .Where(reservation => string.IsNullOrWhiteSpace(query.Search) || MatchesSearch(reservation, query.Search))
                .OrderBy(reservation => reservation.CheckInDate)
                .ThenBy(reservation => reservation.CreatedAt)
                .ToList();

            return Task.FromResult(new PagedResult<Reservation>
            {
                Items = reservations.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = reservations.Count
            });
        }

        public Task<Reservation?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Reservations.FirstOrDefault(reservation => reservation.Id == id && reservation.CompanyId == companyId && !reservation.IsDeleted));
        }

        public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(CompanyExists && companyId == CompanyId);
        }

        public Task<bool> PropertyBelongsToCompanyAsync(Guid propertyId, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(companyId == CompanyId && TenantPropertyIds.Contains(propertyId));
        }

        public Task<bool> GuestBelongsToCompanyAsync(Guid guestId, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(companyId == CompanyId && TenantGuestIds.Contains(guestId));
        }

        public Task<Guest?> GetGuestAsync(Guid guestId, Guid companyId, CancellationToken cancellationToken)
        {
            Guest? guest = companyId == CompanyId && TenantGuestIds.Contains(guestId)
                ? new Guest
                {
                    Id = guestId,
                    CompanyId = companyId,
                    FirstName = "Test",
                    LastName = "Guest",
                    IsActive = true
                }
                : null;

            return Task.FromResult(guest);
        }

        public Task<Conversation?> GetConversationAsync(Guid conversationId, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult<Conversation?>(null);
        }

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(
            Guid companyId,
            Guid guestId,
            DateOnly currentDate,
            DateOnly upcomingThroughDate,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Reservation>>([]);
        }

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsByReferenceAsync(
            Guid companyId,
            Guid guestId,
            string normalizedReference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Reservation>>([]);
        }

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsByPropertyNameAsync(
            Guid companyId,
            Guid guestId,
            DateOnly currentDate,
            DateOnly upcomingThroughDate,
            string normalizedPropertyName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<Reservation>>([]);
        }

        public Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
        {
            Reservations.Add(reservation);
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

        private static bool MatchesSearch(Reservation reservation, string search)
        {
            return (reservation.ExternalReservationReference?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (reservation.ConfirmationNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                || reservation.ReservationSource.Contains(search, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static ReservationService CreateService(FakeReservationRepository repository, FakeCurrentTenantContext? currentTenantContext = null)
    {
        return new ReservationService(
            repository,
            currentTenantContext ?? new FakeCurrentTenantContext(repository.CompanyId),
            new ReservationStatusTransitionPolicy());
    }

    private static async Task AssertTransitionSucceedsAsync(ReservationStatus currentStatus, ReservationStatus targetStatus)
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, status: currentStatus);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.TransitionStatusAsync(reservation.Id, new TransitionReservationStatusRequest { TargetStatus = targetStatus.ToString() }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal(targetStatus, reservation.Status);
        Assert.Single(repository.AuditLogs);
    }

    private static async Task AssertTransitionFailsAsync(ReservationStatus currentStatus, ReservationStatus targetStatus)
    {
        var repository = new FakeReservationRepository();
        var reservation = NewReservation(repository.CompanyId, repository.PropertyId, repository.GuestId, status: currentStatus);
        repository.Reservations.Add(reservation);
        var service = CreateService(repository);

        var response = await service.TransitionStatusAsync(reservation.Id, new TransitionReservationStatusRequest { TargetStatus = targetStatus.ToString() }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Reservation status transition failed.", response.Message);
        Assert.Equal(currentStatus, reservation.Status);
        Assert.Empty(repository.AuditLogs);
    }
}
