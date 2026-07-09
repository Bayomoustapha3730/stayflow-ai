using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.DTOs.Reservations;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class ReservationContextResolverTests
{
    private static readonly DateTimeOffset CurrentTimestamp = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly CurrentDate = DateOnly.FromDateTime(CurrentTimestamp.UtcDateTime);

    [Fact]
    public async Task ResolveAsync_WithVerifiedConversationBinding_ReturnsBoundReservation()
    {
        var repository = new FakeReservationRepository();
        var reservation = repository.NewReservation(status: ReservationStatus.ActiveStay, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(2));
        repository.Reservations.Add(reservation);
        var conversation = repository.NewConversation(reservation);
        repository.Conversations.Add(conversation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            ConversationId = conversation.Id,
            CurrentTimestamp = CurrentTimestamp
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(ReservationContextResolutionMethod.VerifiedConversationBinding, result.ResolutionMethod);
        Assert.Equal(reservation.Id, result.ReservationId);
        Assert.Single(repository.AuditLogs);
    }

    [Fact]
    public async Task ResolveAsync_WithSingleActiveReservation_ResolvesAndBindsConversation()
    {
        var repository = new FakeReservationRepository();
        var reservation = repository.NewReservation(status: ReservationStatus.CheckedIn, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(2));
        repository.Reservations.Add(reservation);
        var conversation = repository.NewConversation();
        repository.Conversations.Add(conversation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            ConversationId = conversation.Id,
            CurrentTimestamp = CurrentTimestamp
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(ReservationContextResolutionMethod.SingleActiveReservation, result.ResolutionMethod);
        Assert.Equal(reservation.Id, conversation.ReservationId);
        Assert.NotNull(conversation.ReservationContextBoundAt);
        Assert.Equal(nameof(ReservationContextResolutionMethod.SingleActiveReservation), conversation.ReservationContextResolutionMethod);
    }

    [Fact]
    public async Task ResolveAsync_WithSingleUpcomingReservationInWindow_Resolves()
    {
        var repository = new FakeReservationRepository();
        var reservation = repository.NewReservation(status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(3), checkOutDate: CurrentDate.AddDays(6));
        repository.Reservations.Add(reservation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(Request(repository.GuestId), CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(ReservationContextResolutionMethod.SingleUpcomingReservation, result.ResolutionMethod);
        Assert.Equal(reservation.Id, result.ReservationId);
    }

    [Fact]
    public async Task ResolveAsync_WithUpcomingReservationOutsideWindow_ReturnsNoEligibleReservation()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(10), checkOutDate: CurrentDate.AddDays(14)));
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(Request(repository.GuestId), CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.NoEligibleReservation, result.Outcome);
        Assert.Null(result.ReservationId);
    }

    [Fact]
    public async Task ResolveAsync_WithSingleFutureReservationOutsideWindowForDateQuestion_Resolves()
    {
        var repository = new FakeReservationRepository();
        var reservation = repository.NewReservation(status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(30), checkOutDate: CurrentDate.AddDays(34));
        repository.Reservations.Add(reservation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            CurrentTimestamp = CurrentTimestamp,
            QuestionCategories = [QuestionContextCategory.CheckIn, QuestionContextCategory.CheckOut]
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(ReservationContextResolutionMethod.SingleFutureReservationForDateQuestion, result.ResolutionMethod);
        Assert.Equal(reservation.Id, result.ReservationId);
    }

    [Fact]
    public async Task ResolveAsync_WithMultipleFutureReservationsForDateQuestion_ReturnsClarification()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(property: repository.NewProperty(name: "Future A"), status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(30), checkOutDate: CurrentDate.AddDays(34)));
        repository.Reservations.Add(repository.NewReservation(property: repository.NewProperty(name: "Future B"), status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(45), checkOutDate: CurrentDate.AddDays(48)));
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            CurrentTimestamp = CurrentTimestamp,
            QuestionCategories = [QuestionContextCategory.CheckIn]
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.ClarificationRequired, result.Outcome);
        Assert.Equal(2, result.CandidateLabels.Count);
    }

    [Fact]
    public async Task ResolveAsync_WithExplicitReservationReference_ResolvesMatchingReservation()
    {
        var repository = new FakeReservationRepository();
        var reservation = repository.NewReservation(status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(2), confirmationNumber: "CONF-777");
        repository.Reservations.Add(reservation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            CurrentTimestamp = CurrentTimestamp,
            ExplicitReservationReference = " conf-777 "
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(ReservationContextResolutionMethod.ExplicitReservationReference, result.ResolutionMethod);
        Assert.Equal(reservation.Id, result.ReservationId);
    }

    [Fact]
    public async Task ResolveAsync_WithExplicitReferenceForAnotherGuest_ReturnsNoEligibleReservation()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(
            guestId: Guid.NewGuid(),
            status: ReservationStatus.Confirmed,
            checkInDate: CurrentDate.AddDays(2),
            confirmationNumber: "CONF-OTHER"));
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            CurrentTimestamp = CurrentTimestamp,
            ExplicitReservationReference = "CONF-OTHER"
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.NoEligibleReservation, result.Outcome);
    }

    [Fact]
    public async Task ResolveAsync_WithExplicitReferenceForAnotherTenant_ReturnsNoEligibleReservation()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(
            companyId: Guid.NewGuid(),
            status: ReservationStatus.Confirmed,
            checkInDate: CurrentDate.AddDays(2),
            confirmationNumber: "CONF-TENANT-B"));
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            CurrentTimestamp = CurrentTimestamp,
            ExplicitReservationReference = "CONF-TENANT-B"
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.NoEligibleReservation, result.Outcome);
    }

    [Fact]
    public async Task ResolveAsync_WithExplicitPropertyName_NarrowsCandidates()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(property: repository.NewProperty(name: "Lavington Studio"), status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(2)));
        var matchingReservation = repository.NewReservation(property: repository.NewProperty(name: "Karen Cottage"), status: ReservationStatus.Confirmed, checkInDate: CurrentDate.AddDays(3));
        repository.Reservations.Add(matchingReservation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            CurrentTimestamp = CurrentTimestamp,
            ExplicitPropertyName = "Karen"
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.Resolved, result.Outcome);
        Assert.Equal(ReservationContextResolutionMethod.ExplicitPropertyName, result.ResolutionMethod);
        Assert.Equal(matchingReservation.Id, result.ReservationId);
    }

    [Fact]
    public async Task ResolveAsync_WithMultipleEligibleReservations_ReturnsSafeClarificationLabels()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(property: repository.NewProperty(name: "Westlands Suite", city: "Nairobi"), status: ReservationStatus.ActiveStay, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(2), internalNotes: "door-code-secret"));
        repository.Reservations.Add(repository.NewReservation(property: repository.NewProperty(name: "Diani Villa", city: "Diani"), status: ReservationStatus.ActiveStay, checkInDate: CurrentDate.AddDays(-2), checkOutDate: CurrentDate.AddDays(3), internalNotes: "manager-only"));
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(Request(repository.GuestId), CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.ClarificationRequired, result.Outcome);
        Assert.Equal(repository.CompanyId, result.CompanyId);
        Assert.Equal(repository.GuestId, result.GuestId);
        Assert.Equal(2, result.CandidateLabels.Count);
        Assert.All(result.CandidateLabels, label =>
        {
            Assert.False(string.IsNullOrWhiteSpace(label.PropertyName));
            Assert.False(string.IsNullOrWhiteSpace(label.City));
        });
        Assert.DoesNotContain("secret", repository.AuditLogs.Single().Details, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manager-only", repository.AuditLogs.Single().Details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveAsync_WithConflictingConversationGuest_ReturnsEscalation()
    {
        var repository = new FakeReservationRepository();
        var conversation = repository.NewConversation();
        repository.Conversations.Add(conversation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = Guid.NewGuid(),
            ConversationId = conversation.Id,
            CurrentTimestamp = CurrentTimestamp
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.EscalationRequired, result.Outcome);
        Assert.Equal("ConflictingGuestIdentity", result.EscalationReason);
    }

    [Fact]
    public async Task ResolveAsync_WithConflictingChannelIdentity_ReturnsEscalation()
    {
        var repository = new FakeReservationRepository();
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            GuestId = repository.GuestId,
            CurrentTimestamp = CurrentTimestamp,
            ChannelIdentity = "+254 700 999999"
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.EscalationRequired, result.Outcome);
        Assert.Equal("ConflictingChannelIdentity", result.EscalationReason);
    }

    [Fact]
    public async Task ResolveAsync_WithMissingTenantContext_FailsSafely()
    {
        var repository = new FakeReservationRepository();
        var resolver = CreateResolver(repository, new FakeCurrentTenantContext(null));

        var result = await resolver.ResolveAsync(Request(repository.GuestId), CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.EscalationRequired, result.Outcome);
        Assert.Equal("TenantContextUnavailable", result.EscalationReason);
        Assert.Empty(repository.CandidateQueryCompanyIds);
    }

    [Fact]
    public async Task ResolveAsync_WithUnauthenticatedTenantContext_FailsSafely()
    {
        var repository = new FakeReservationRepository();
        var resolver = CreateResolver(repository, new FakeCurrentTenantContext(repository.CompanyId, isAuthenticated: false));

        var result = await resolver.ResolveAsync(Request(repository.GuestId), CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.EscalationRequired, result.Outcome);
        Assert.Equal("TenantContextUnavailable", result.EscalationReason);
        Assert.Empty(repository.CandidateQueryCompanyIds);
    }

    [Fact]
    public async Task ResolveAsync_NeverReturnsAnotherTenantsCandidates()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(companyId: Guid.NewGuid(), status: ReservationStatus.ActiveStay, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(1)));
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(Request(repository.GuestId), CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.NoEligibleReservation, result.Outcome);
        Assert.All(repository.CandidateQueryCompanyIds, companyId => Assert.Equal(repository.CompanyId, companyId));
    }

    [Fact]
    public async Task ResolveAsync_IgnoresCancelledNoShowAndSoftDeletedReservations()
    {
        var repository = new FakeReservationRepository();
        repository.Reservations.Add(repository.NewReservation(status: ReservationStatus.Cancelled, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(1)));
        repository.Reservations.Add(repository.NewReservation(status: ReservationStatus.NoShow, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(1)));
        repository.Reservations.Add(repository.NewReservation(status: ReservationStatus.ActiveStay, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(1), isDeleted: true));
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(Request(repository.GuestId), CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.NoEligibleReservation, result.Outcome);
    }

    [Fact]
    public async Task ResolveAsync_WithInvalidConversationBinding_DoesNotReuseBinding()
    {
        var repository = new FakeReservationRepository();
        var cancelledReservation = repository.NewReservation(status: ReservationStatus.Cancelled, checkInDate: CurrentDate.AddDays(-1), checkOutDate: CurrentDate.AddDays(1));
        repository.Reservations.Add(cancelledReservation);
        var conversation = repository.NewConversation(cancelledReservation);
        repository.Conversations.Add(conversation);
        var resolver = CreateResolver(repository);

        var result = await resolver.ResolveAsync(new ReservationContextRequest
        {
            ConversationId = conversation.Id,
            CurrentTimestamp = CurrentTimestamp
        }, CancellationToken.None);

        Assert.Equal(ReservationContextResolutionOutcome.NoEligibleReservation, result.Outcome);
        Assert.NotEqual(ReservationContextResolutionMethod.VerifiedConversationBinding, result.ResolutionMethod);
    }

    private static ReservationContextRequest Request(Guid guestId)
    {
        return new ReservationContextRequest
        {
            GuestId = guestId,
            CurrentTimestamp = CurrentTimestamp
        };
    }

    private static ReservationContextResolver CreateResolver(FakeReservationRepository repository, FakeCurrentTenantContext? currentTenantContext = null)
    {
        return new ReservationContextResolver(
            repository,
            currentTenantContext ?? new FakeCurrentTenantContext(repository.CompanyId),
            Options.Create(new ReservationContextOptions { PreArrivalWindowDays = 7 }),
            NullLogger<ReservationContextResolver>.Instance);
    }

    private sealed class FakeCurrentTenantContext(
        Guid? companyId,
        bool isAuthenticated = true,
        string? correlationId = null) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = correlationId ?? "resolver-test-correlation";
        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class FakeReservationRepository : IReservationRepository
    {
        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid PropertyId { get; } = Guid.NewGuid();
        public Guid GuestId { get; } = Guid.NewGuid();
        public List<Reservation> Reservations { get; } = [];
        public List<Conversation> Conversations { get; } = [];
        public List<AuditLog> AuditLogs { get; } = [];
        public List<Guid> CandidateQueryCompanyIds { get; } = [];

        public FakeReservationRepository()
        {
            Guests.Add(new Guest
            {
                Id = GuestId,
                CompanyId = CompanyId,
                FirstName = "Amina",
                LastName = "Otieno",
                Email = "amina@example.com",
                PhoneNumber = "+254700123456",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            });
        }

        private List<Guest> Guests { get; } = [];

        public Task<PagedResult<Reservation>> GetAsync(Guid companyId, ReservationQueryParameters query, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PagedResult<Reservation>
            {
                Items = [],
                PageNumber = query.NormalizedPageNumber,
                PageSize = query.NormalizedPageSize,
                TotalCount = 0
            });
        }

        public Task<Reservation?> GetByIdAsync(Guid id, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Reservations.FirstOrDefault(reservation => reservation.Id == id && reservation.CompanyId == companyId && !reservation.IsDeleted));
        }

        public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(companyId == CompanyId);
        }

        public Task<bool> PropertyBelongsToCompanyAsync(Guid propertyId, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(companyId == CompanyId && propertyId == PropertyId);
        }

        public Task<bool> GuestBelongsToCompanyAsync(Guid guestId, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(companyId == CompanyId && Guests.Any(guest => guest.Id == guestId && guest.CompanyId == companyId && guest.IsActive));
        }

        public Task<Guest?> GetGuestAsync(Guid guestId, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Guests.FirstOrDefault(guest => guest.Id == guestId && guest.CompanyId == companyId && guest.IsActive));
        }

        public Task<Conversation?> GetConversationAsync(Guid conversationId, Guid companyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Conversations.FirstOrDefault(conversation => conversation.Id == conversationId && conversation.CompanyId == companyId));
        }

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, CancellationToken cancellationToken)
        {
            CandidateQueryCompanyIds.Add(companyId);
            return Task.FromResult<IReadOnlyCollection<Reservation>>(Eligible(companyId, guestId, currentDate, upcomingThroughDate).ToList());
        }

        public Task<IReadOnlyCollection<Reservation>> GetFutureReservationsForGuestAsync(Guid companyId, Guid guestId, DateOnly currentDate, CancellationToken cancellationToken)
        {
            CandidateQueryCompanyIds.Add(companyId);
            return Task.FromResult<IReadOnlyCollection<Reservation>>(Reservations
                .Where(reservation => reservation.CompanyId == companyId && reservation.PrimaryGuestId == guestId && reservation.IsActive && !reservation.IsDeleted)
                .Where(reservation => reservation.Status is ReservationStatus.Confirmed or ReservationStatus.PreArrival or ReservationStatus.ReadyForCheckIn)
                .Where(reservation => reservation.CheckInDate >= currentDate)
                .OrderBy(reservation => reservation.CheckInDate)
                .ToList());
        }

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsByReferenceAsync(Guid companyId, Guid guestId, string normalizedReference, CancellationToken cancellationToken)
        {
            CandidateQueryCompanyIds.Add(companyId);
            return Task.FromResult<IReadOnlyCollection<Reservation>>(Reservations
                .Where(reservation => reservation.CompanyId == companyId && reservation.PrimaryGuestId == guestId && !reservation.IsDeleted)
                .Where(reservation => reservation.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow)
                .Where(reservation =>
                    string.Equals(reservation.ExternalReservationReference?.ToUpperInvariant(), normalizedReference, StringComparison.Ordinal)
                    || string.Equals(reservation.ConfirmationNumber?.ToUpperInvariant(), normalizedReference, StringComparison.Ordinal))
                .ToList());
        }

        public Task<IReadOnlyCollection<Reservation>> GetEligibleReservationsByPropertyNameAsync(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate, string normalizedPropertyName, CancellationToken cancellationToken)
        {
            CandidateQueryCompanyIds.Add(companyId);
            return Task.FromResult<IReadOnlyCollection<Reservation>>(Eligible(companyId, guestId, currentDate, upcomingThroughDate)
                .Where(reservation => reservation.Property.Name.Contains(normalizedPropertyName, StringComparison.OrdinalIgnoreCase))
                .ToList());
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

        public Conversation NewConversation(Reservation? reservation = null)
        {
            return new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = reservation?.PropertyId ?? PropertyId,
                GuestId = GuestId,
                ReservationId = reservation?.Id,
                Reservation = reservation,
                Channel = "WhatsApp",
                ExternalThreadId = "whatsapp:+254700123456",
                Status = "Open",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        public Reservation NewReservation(
            Guid? companyId = null,
            Guid? guestId = null,
            Property? property = null,
            ReservationStatus status = ReservationStatus.Confirmed,
            DateOnly? checkInDate = null,
            DateOnly? checkOutDate = null,
            string confirmationNumber = "CONF-001",
            string externalReference = "EXT-001",
            string? internalNotes = null,
            bool isDeleted = false)
        {
            var reservationProperty = property ?? NewProperty();
            var reservationGuestId = guestId ?? GuestId;
            var resolvedCompanyId = companyId ?? CompanyId;

            return new Reservation
            {
                Id = Guid.NewGuid(),
                CompanyId = resolvedCompanyId,
                PropertyId = reservationProperty.Id,
                PrimaryGuestId = reservationGuestId,
                Property = reservationProperty,
                PrimaryGuest = new Guest { Id = reservationGuestId, CompanyId = resolvedCompanyId, FirstName = "Amina", LastName = "Otieno" },
                ExternalReservationReference = externalReference,
                ReservationSource = "Airbnb",
                ConfirmationNumber = confirmationNumber,
                CheckInDate = checkInDate ?? CurrentDate,
                CheckOutDate = checkOutDate ?? CurrentDate.AddDays(3),
                Adults = 2,
                Children = 0,
                TotalGuestCount = 2,
                Status = status,
                Currency = "KES",
                BookingAmount = 1000,
                InternalNotes = internalNotes,
                IsActive = true,
                IsDeleted = isDeleted,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        public Property NewProperty(string name = "Demo Apartment", string city = "Nairobi")
        {
            return new Property
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                Name = name,
                AddressLine1 = "Demo Road",
                City = city,
                CountryCode = "KE",
                TimeZone = "Africa/Nairobi",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        private IEnumerable<Reservation> Eligible(Guid companyId, Guid guestId, DateOnly currentDate, DateOnly upcomingThroughDate)
        {
            return Reservations
                .Where(reservation => reservation.CompanyId == companyId && reservation.PrimaryGuestId == guestId && !reservation.IsDeleted)
                .Where(reservation => reservation.Status is not ReservationStatus.Cancelled and not ReservationStatus.NoShow)
                .Where(reservation => IsActive(reservation, currentDate) || IsUpcoming(reservation, currentDate, upcomingThroughDate))
                .OrderBy(reservation => reservation.CheckInDate);
        }

        private static bool IsActive(Reservation reservation, DateOnly currentDate)
        {
            return reservation.Status is ReservationStatus.ReadyForCheckIn or ReservationStatus.CheckedIn or ReservationStatus.ActiveStay or ReservationStatus.CheckOutPending
                && reservation.CheckInDate <= currentDate
                && reservation.CheckOutDate >= currentDate;
        }

        private static bool IsUpcoming(Reservation reservation, DateOnly currentDate, DateOnly upcomingThroughDate)
        {
            return reservation.Status is ReservationStatus.Confirmed or ReservationStatus.PreArrival or ReservationStatus.ReadyForCheckIn
                && reservation.CheckInDate >= currentDate
                && reservation.CheckInDate <= upcomingThroughDate;
        }
    }
}
