using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class AIContextBuilderTests
{
    private static readonly DateTimeOffset CurrentTimestamp = new(2026, 8, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BuildAsync_WiFiQuestionIncludesWiFiContext()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "WiFi details", "Use the guest WiFi network in the lounge."));
        fixture.Property.PropertyAmenities.Add(Amenity("High speed WiFi"));

        var result = await fixture.BuildAsync("What is the wifi?");

        Assert.Equal(AIContextBuildOutcome.Ready, result.Outcome);
        Assert.Contains(QuestionContextCategory.WiFi, result.QuestionCategories);
        Assert.Contains(result.Context!.Knowledge.Articles, article => article.Title == "WiFi details");
        Assert.Contains(result.Context.Knowledge.Amenities, amenity => amenity.Name == "High speed WiFi");
    }

    [Fact]
    public async Task BuildAsync_ParkingQuestionIncludesParkingContext()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Parking", "Secure parking is available."));
        fixture.Property.PropertyAmenities.Add(Amenity("Basement parking"));
        fixture.Property.PropertyHouseRules.Add(HouseRule("Parking rule", "Park only in bay 4."));

        var result = await fixture.BuildAsync("Where can I park my car?");

        Assert.Contains(result.Context!.Knowledge.Articles, article => article.Title == "Parking");
        Assert.Contains(result.Context.Knowledge.Amenities, amenity => amenity.Name == "Basement parking");
        Assert.Contains(result.Context.Knowledge.HouseRules, rule => rule.Title == "Parking rule");
    }

    [Fact]
    public async Task BuildAsync_HouseRuleQuestionIncludesHouseRules()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyHouseRules.Add(HouseRule("Quiet hours", "No loud music after 10pm."));

        var result = await fixture.BuildAsync("What are the house rules?");

        Assert.Contains(QuestionContextCategory.HouseRules, result.QuestionCategories);
        Assert.Contains(result.Context!.Knowledge.HouseRules, rule => rule.Title == "Quiet hours");
    }

    [Fact]
    public async Task BuildAsync_RestaurantQuestionIncludesRecommendations()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyRecommendations.Add(Recommendation("Tamu Grill", "Restaurant"));

        var result = await fixture.BuildAsync("Any restaurant for dinner nearby?");

        Assert.Contains(result.Context!.Knowledge.Recommendations, recommendation => recommendation.Name == "Tamu Grill");
    }

    [Fact]
    public async Task BuildAsync_EmergencyQuestionIncludesEmergencyContacts()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyEmergencyContacts.Add(EmergencyContact("Front Desk", "Emergency"));

        var result = await fixture.BuildAsync("This is an emergency, who do I call?");

        Assert.Contains(result.Context!.Knowledge.EmergencyContacts, contact => contact.Name == "Front Desk");
    }

    [Fact]
    public async Task BuildAsync_CheckInQuestionIncludesCheckInContext()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Check in guide", "Check in starts at 3pm."));

        var result = await fixture.BuildAsync("What time is check in?");

        Assert.Contains(QuestionContextCategory.CheckIn, result.QuestionCategories);
        Assert.Equal("PreArrival", result.Context!.Reservation!.CurrentStayPhase);
        Assert.Contains(result.Context.Knowledge.Articles, article => article.Title == "Check in guide");
    }

    [Fact]
    public async Task BuildAsync_CheckOutQuestionIncludesCheckoutContext()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Checkout", "Checkout is at 10am."));

        var result = await fixture.BuildAsync("When is checkout?");

        Assert.Contains(QuestionContextCategory.CheckOut, result.QuestionCategories);
        Assert.Contains(result.Context!.Knowledge.Articles, article => article.Title == "Checkout");
    }

    [Fact]
    public async Task BuildAsync_LaundryQuestionIncludesLaundryContext()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Laundry", "The laundry room is near reception."));
        fixture.Property.PropertyAmenities.Add(Amenity("Laundry washer"));

        var result = await fixture.BuildAsync("Can I use the laundry?");

        Assert.Contains(result.Context!.Knowledge.Articles, article => article.Title == "Laundry");
        Assert.Contains(result.Context.Knowledge.Amenities, amenity => amenity.Name == "Laundry washer");
    }

    [Fact]
    public async Task BuildAsync_GeneralQuestionDoesNotLoadAllPropertyKnowledge()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "WiFi", "WiFi information."));
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Parking", "Parking information."));

        var result = await fixture.BuildAsync("Hello there");

        Assert.Equal([QuestionContextCategory.General], result.QuestionCategories);
        Assert.Empty(result.Context!.Knowledge.Articles);
    }

    [Fact]
    public async Task BuildAsync_MultipleReservationCandidatesReturnsClarificationRequired()
    {
        var fixture = new Fixture();
        fixture.Resolver.Result = new ReservationContextResolutionResult
        {
            Outcome = ReservationContextResolutionOutcome.ClarificationRequired,
            CandidateLabels = [new ReservationCandidateLabel { PropertyName = "A", City = "Nairobi", CheckInDate = new DateOnly(2026, 8, 10) }],
            Message = "Clarify"
        };

        var result = await fixture.BuildAsync("What is the wifi?");

        Assert.Equal(AIContextBuildOutcome.ClarificationRequired, result.Outcome);
        Assert.Single(result.CandidateLabels);
        Assert.Null(result.Context);
    }

    [Fact]
    public async Task BuildAsync_ReservationEscalationReturnsEscalationRequired()
    {
        var fixture = new Fixture();
        fixture.Resolver.Result = new ReservationContextResolutionResult
        {
            Outcome = ReservationContextResolutionOutcome.EscalationRequired,
            EscalationReason = "ConflictingGuestIdentity",
            Message = "Escalate"
        };

        var result = await fixture.BuildAsync("What is the wifi?");

        Assert.Equal(AIContextBuildOutcome.EscalationRequired, result.Outcome);
        Assert.Equal("ConflictingGuestIdentity", result.EscalationReason);
        Assert.Null(result.Context);
    }

    [Fact]
    public async Task BuildAsync_NoEligibleReservationExcludesStaySpecificContext()
    {
        var fixture = new Fixture();
        fixture.Resolver.Result = new ReservationContextResolutionResult
        {
            Outcome = ReservationContextResolutionOutcome.NoEligibleReservation,
            CompanyId = fixture.CompanyId,
            GuestId = fixture.GuestId,
            Message = "No eligible"
        };

        var result = await fixture.BuildAsync("What is the door code?");

        Assert.Equal(AIContextBuildOutcome.NoEligibleReservation, result.Outcome);
        Assert.Null(result.Context!.Reservation);
        Assert.Null(result.Context.Property);
        Assert.True(result.Context.Safety.RequiresPropertyAccessAuthorization);
        Assert.Equal(fixture.CompanyId, result.Metadata.CompanyId);
        Assert.Equal(fixture.GuestId, result.Metadata.GuestId);
        Assert.Null(result.Metadata.ReservationId);
        Assert.Null(result.Metadata.PropertyId);
    }

    [Fact]
    public async Task BuildAsync_ResolvedReservationBuildsStructuredContext()
    {
        var fixture = new Fixture();

        var result = await fixture.BuildAsync("What is check in?");

        Assert.Equal(AIContextBuildOutcome.Ready, result.Outcome);
        Assert.NotNull(result.Context!.Guest);
        Assert.NotNull(result.Context.Reservation);
        Assert.NotNull(result.Context.Property);
        Assert.NotNull(result.Context.Knowledge);
        Assert.True(result.Context.Safety.ReservationContextResolved);
        Assert.Equal(fixture.CompanyId, result.Metadata.CompanyId);
        Assert.Equal(fixture.GuestId, result.Metadata.GuestId);
        Assert.Equal(fixture.ReservationId, result.Metadata.ReservationId);
        Assert.Equal(fixture.PropertyId, result.Metadata.PropertyId);
    }

    [Fact]
    public async Task BuildAsync_InternalGuestNotesReservationInternalNotesBookingAmountAndCurrencyAreExcluded()
    {
        var fixture = new Fixture();
        fixture.Guest.Notes = "guest-secret";
        fixture.Reservation.InternalNotes = "reservation-secret";
        fixture.Reservation.BookingAmount = 9000;
        fixture.Reservation.Currency = "KES";

        var result = await fixture.BuildAsync("What is check in?");
        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Context);

        Assert.DoesNotContain("guest-secret", serialized);
        Assert.DoesNotContain("reservation-secret", serialized);
        Assert.Null(typeof(AIReservationContext).GetProperty("BookingAmount"));
        Assert.Null(typeof(AIReservationContext).GetProperty("Currency"));
    }

    [Fact]
    public async Task BuildAsync_PropertyAccessQuestionSetsAuthorizationFlagAndExcludesSecrets()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Door code", "The door code is 1234."));

        var result = await fixture.BuildAsync("What is the door code?");
        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Context);

        Assert.True(result.Context!.Safety.RequiresPropertyAccessAuthorization);
        Assert.Empty(result.Context.Knowledge.Articles);
        Assert.DoesNotContain("1234", serialized);
    }

    [Fact]
    public async Task BuildAsync_TenantACannotBuildContextFromTenantBGuest()
    {
        var fixture = new Fixture();
        fixture.Guest.CompanyId = Guid.NewGuid();

        var result = await fixture.BuildAsync("What is the wifi?");

        Assert.Equal(AIContextBuildOutcome.EscalationRequired, result.Outcome);
        Assert.Equal("ResolvedContextValidationFailed", result.EscalationReason);
        Assert.Equal(fixture.CompanyId, result.Metadata.CompanyId);
        Assert.Null(result.Metadata.GuestId);
        Assert.Null(result.Metadata.ReservationId);
        Assert.Null(result.Metadata.PropertyId);
    }

    [Fact]
    public async Task BuildAsync_TenantACannotRetrieveTenantBPropertyKnowledge()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "WiFi A", "Tenant A WiFi."));
        fixture.Property.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PropertyId = fixture.PropertyId,
            Title = "WiFi B",
            Content = "Tenant B WiFi.",
            IsActive = true
        });

        var result = await fixture.BuildAsync("What is the wifi?");

        Assert.Contains(result.Context!.Knowledge.Articles, article => article.Title == "WiFi A");
        Assert.DoesNotContain(result.Context.Knowledge.Articles, article => article.Title == "WiFi B");
    }

    [Fact]
    public async Task BuildAsync_InactiveKnowledgeIsExcluded()
    {
        var fixture = new Fixture();
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "WiFi active", "Active WiFi."));
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "WiFi inactive", "Inactive WiFi.", isActive: false));

        var result = await fixture.BuildAsync("What is the wifi?");

        Assert.Contains(result.Context!.Knowledge.Articles, article => article.Title == "WiFi active");
        Assert.DoesNotContain(result.Context.Knowledge.Articles, article => article.Title == "WiFi inactive");
    }

    [Fact]
    public async Task BuildAsync_ContextLimitsAreEnforced()
    {
        var fixture = new Fixture(new AIContextOptions { MaxKnowledgeArticles = 1, MaxRecommendations = 1, MaxHouseRules = 1, MaxEmergencyContacts = 1 });
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Emergency 1", "Emergency contact one."));
        fixture.Property.PropertyKnowledgeArticles.Add(Article(fixture, "Emergency 2", "Emergency contact two."));
        fixture.Property.PropertyRecommendations.Add(Recommendation("Hospital A", "Emergency"));
        fixture.Property.PropertyRecommendations.Add(Recommendation("Hospital B", "Emergency"));
        fixture.Property.PropertyHouseRules.Add(HouseRule("Emergency rule A", "Emergency A."));
        fixture.Property.PropertyHouseRules.Add(HouseRule("Emergency rule B", "Emergency B."));
        fixture.Property.PropertyEmergencyContacts.Add(EmergencyContact("A", "Emergency"));
        fixture.Property.PropertyEmergencyContacts.Add(EmergencyContact("B", "Emergency"));

        var result = await fixture.BuildAsync("Emergency help");

        Assert.Single(result.Context!.Knowledge.Articles);
        Assert.Single(result.Context.Knowledge.Recommendations);
        Assert.Single(result.Context.Knowledge.HouseRules);
        Assert.Single(result.Context.Knowledge.EmergencyContacts);
    }

    [Fact]
    public async Task BuildAsync_ConversationTenantMismatchFailsSafely()
    {
        var fixture = new Fixture();
        fixture.Conversation.CompanyId = Guid.NewGuid();

        var result = await fixture.BuildAsync("What is the wifi?", fixture.Conversation.Id);

        Assert.Equal(AIContextBuildOutcome.EscalationRequired, result.Outcome);
        Assert.Equal("ConversationContextValidationFailed", result.EscalationReason);
    }

    [Fact]
    public void ClassifierSupportsMultipleCategories()
    {
        var classifier = new KeywordQuestionRelevanceClassifier();

        var categories = classifier.Classify("Can I get the WiFi and parking details?");

        Assert.Contains(QuestionContextCategory.WiFi, categories);
        Assert.Contains(QuestionContextCategory.Parking, categories);
    }

    [Fact]
    public async Task BuildAsync_GuestQuestionIsNotWrittenToAuditLogs()
    {
        var fixture = new Fixture();

        await fixture.BuildAsync("secret question content about wifi");

        Assert.DoesNotContain(fixture.Repository.AuditLogs, log => log.Details?.Contains("secret question content", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static PropertyKnowledgeArticle Article(Fixture fixture, string title, string content, bool isActive = true)
    {
        return new PropertyKnowledgeArticle
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            PropertyId = fixture.PropertyId,
            Title = title,
            Content = content,
            IsActive = isActive
        };
    }

    private static PropertyAmenity Amenity(string name)
    {
        return new PropertyAmenity { Id = Guid.NewGuid(), Name = name, IsActive = true };
    }

    private static PropertyHouseRule HouseRule(string title, string description)
    {
        return new PropertyHouseRule { Id = Guid.NewGuid(), Title = title, Description = description, IsActive = true };
    }

    private static PropertyRecommendation Recommendation(string name, string category)
    {
        return new PropertyRecommendation { Id = Guid.NewGuid(), Name = name, Category = category, Description = $"{category} recommendation", IsActive = true };
    }

    private static PropertyEmergencyContact EmergencyContact(string name, string role)
    {
        return new PropertyEmergencyContact { Id = Guid.NewGuid(), Name = name, Role = role, PhoneNumber = "+254700000000", IsActive = true };
    }

    private sealed class Fixture
    {
        public Fixture(AIContextOptions? options = null)
        {
            Property = new Property
            {
                Id = PropertyId,
                CompanyId = CompanyId,
                Name = "Demo Stay",
                AddressLine1 = "Demo Road",
                City = "Nairobi",
                CountryCode = "KE",
                TimeZone = "Africa/Nairobi",
                Description = "Demo property",
                IsActive = true
            };
            Guest = new Guest
            {
                Id = GuestId,
                CompanyId = CompanyId,
                FirstName = "Amina",
                LastName = "Otieno",
                PreferredLanguage = "en",
                CountryCode = "KE",
                IsActive = true
            };
            Reservation = new Reservation
            {
                Id = ReservationId,
                CompanyId = CompanyId,
                PropertyId = PropertyId,
                PrimaryGuestId = GuestId,
                CheckInDate = new DateOnly(2026, 8, 12),
                CheckOutDate = new DateOnly(2026, 8, 15),
                Adults = 2,
                Children = 1,
                TotalGuestCount = 3,
                Status = ReservationStatus.Confirmed,
                ReservationSource = "Airbnb",
                IsActive = true
            };
            Conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                CompanyId = CompanyId,
                PropertyId = PropertyId,
                GuestId = GuestId,
                ReservationId = ReservationId,
                Channel = "WhatsApp",
                Status = "Open"
            };
            Repository = new FakeAIContextRepository(this);
            Resolver = new FakeReservationContextResolver(this);
            Builder = new AIContextBuilder(
                Repository,
                Resolver,
                new KeywordQuestionRelevanceClassifier(),
                new FakeCurrentTenantContext(CompanyId),
                Options.Create(options ?? new AIContextOptions()),
                NullLogger<AIContextBuilder>.Instance);
        }

        public Guid CompanyId { get; } = Guid.NewGuid();
        public Guid PropertyId { get; } = Guid.NewGuid();
        public Guid GuestId { get; } = Guid.NewGuid();
        public Guid ReservationId { get; } = Guid.NewGuid();
        public Property Property { get; }
        public Guest Guest { get; }
        public Reservation Reservation { get; }
        public Conversation Conversation { get; }
        public FakeAIContextRepository Repository { get; }
        public FakeReservationContextResolver Resolver { get; }
        private AIContextBuilder Builder { get; }

        public Task<AIContextBuildResult> BuildAsync(string question, Guid? conversationId = null)
        {
            return Builder.BuildAsync(new AIContextRequest
            {
                GuestQuestion = question,
                GuestId = GuestId,
                ConversationId = conversationId,
                CurrentTimestamp = CurrentTimestamp
            }, CancellationToken.None);
        }
    }

    private sealed class FakeCurrentTenantContext(Guid? companyId) : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "ai-context-test-correlation";
        public bool IsAuthenticated { get; } = true;
    }

    private sealed class FakeReservationContextResolver(Fixture fixture) : IReservationContextResolver
    {
        public ReservationContextResolutionResult Result { get; set; } = ReservationContextResolutionResult.Resolved(
            fixture.CompanyId,
            fixture.GuestId,
            fixture.ReservationId,
            fixture.PropertyId,
            ReservationContextResolutionMethod.SingleUpcomingReservation,
            CurrentTimestamp);

        public Task<ReservationContextResolutionResult> ResolveAsync(ReservationContextRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAIContextRepository(Fixture fixture) : IAIContextRepository
    {
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
        {
            return Task.FromResult(fixture.Guest.CompanyId == companyId && fixture.Guest.Id == guestId && fixture.Guest.IsActive ? fixture.Guest : null);
        }

        public Task<int> CountCompletedReservationsForGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }

        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(fixture.Reservation.CompanyId == companyId && fixture.Reservation.Id == reservationId && fixture.Reservation.IsActive ? fixture.Reservation : null);
        }

        public Task<Property?> GetPropertyContextAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken)
        {
            return Task.FromResult(fixture.Property.CompanyId == companyId && fixture.Property.Id == propertyId && fixture.Property.IsActive ? fixture.Property : null);
        }

        public Task<Conversation?> GetConversationAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken)
        {
            return Task.FromResult(fixture.Conversation.CompanyId == companyId && fixture.Conversation.Id == conversationId ? fixture.Conversation : null);
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
    }
}
