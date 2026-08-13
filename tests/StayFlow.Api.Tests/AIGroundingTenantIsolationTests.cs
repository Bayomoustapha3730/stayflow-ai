using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Tests;

/// <summary>
/// Proves the guest-message grounding path is tenant and property scoped using the real
/// EF repositories rather than in-memory fakes.
/// </summary>
public sealed class AIGroundingTenantIsolationTests : IAsyncLifetime
{
    private const string CompanyACheckIn = "Check-in begins at 3:00 PM.";
    private const string CompanyASecondPropertyCheckIn = "Check-in begins at 5:00 PM.";
    private const string CompanyBCheckIn = "Check-in begins at 4:00 PM.";

    private DbContextOptions<ApplicationDbContext> options = null!;
    private readonly Guid companyAId = Guid.NewGuid();
    private readonly Guid companyBId = Guid.NewGuid();
    private readonly Guid propertyA1Id = Guid.NewGuid();
    private readonly Guid propertyA2Id = Guid.NewGuid();
    private readonly Guid propertyBId = Guid.NewGuid();
    private readonly Guid conversationA1Id = Guid.NewGuid();
    private readonly Guid conversationA2Id = Guid.NewGuid();
    private readonly Guid conversationBId = Guid.NewGuid();
    private readonly Guid conversationWithoutReservationId = Guid.NewGuid();
    private readonly Guid reservationAId = Guid.NewGuid();
    private readonly Guid reservationBId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ai-grounding-isolation-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using var seed = new ApplicationDbContext(options);

        seed.Companies.AddRange(
            NewCompany(companyAId, "Company A", "company-a"),
            NewCompany(companyBId, "Company B", "company-b"));

        seed.Properties.AddRange(
            NewProperty(propertyA1Id, companyAId, "Coast Villa A1"),
            NewProperty(propertyA2Id, companyAId, "Coast Villa A2"),
            NewProperty(propertyBId, companyBId, "Beach House B"));

        var guestAId = Guid.NewGuid();
        var guestBId = Guid.NewGuid();
        seed.Guests.AddRange(
            NewGuest(guestAId, companyAId, "Ada", "Lovelace"),
            NewGuest(guestBId, companyBId, "Grace", "Hopper"));

        seed.Reservations.AddRange(
            NewReservation(reservationAId, companyAId, propertyA1Id, guestAId, "AAA-111"),
            NewReservation(reservationBId, companyBId, propertyBId, guestBId, "BBB-222"));

        seed.Conversations.AddRange(
            NewConversation(conversationA1Id, companyAId, guestAId, propertyA1Id, reservationAId),
            NewConversation(conversationA2Id, companyAId, guestAId, propertyA2Id, null),
            NewConversation(conversationBId, companyBId, guestBId, propertyBId, reservationBId),
            NewConversation(conversationWithoutReservationId, companyAId, guestAId, propertyA1Id, null));

        seed.ConversationMessages.AddRange(
            NewGuestMessage(companyAId, conversationA1Id, "What time is check-in?"),
            NewGuestMessage(companyAId, conversationA2Id, "What time is check-in?"),
            NewGuestMessage(companyBId, conversationBId, "What time is check-in?"),
            NewGuestMessage(companyAId, conversationWithoutReservationId, "What time is check-in?"));

        seed.PropertyKnowledgeArticles.AddRange(
            NewKnowledge(companyAId, propertyA1Id, "Check-in Times", CompanyACheckIn),
            NewKnowledge(companyAId, propertyA2Id, "Check-in Times", CompanyASecondPropertyCheckIn),
            NewKnowledge(companyBId, propertyBId, "Check-in Times", CompanyBCheckIn),
            NewKnowledge(companyAId, propertyA1Id, "Unapproved Draft", "Check-in begins at 9:00 PM.", isApproved: false),
            NewKnowledge(companyAId, propertyA1Id, "Retired Policy", "Check-in begins at 10:00 PM.", isActive: false));

        await seed.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task BuildAsync_ForCompanyA_UsesOnlyCompanyAPropertyKnowledge()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyAId, conversationA1Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(companyAId, context!.TenantId);
        Assert.Equal(propertyA1Id, context.PropertyId);

        var knowledgeContent = string.Join("\n", context.ApprovedKnowledgeItems.Select(item => item.Content));
        Assert.Contains(CompanyACheckIn, knowledgeContent);
        Assert.DoesNotContain(CompanyBCheckIn, knowledgeContent);
        Assert.DoesNotContain(CompanyASecondPropertyCheckIn, knowledgeContent);
    }

    [Fact]
    public async Task BuildAsync_ForCompanyB_UsesOnlyCompanyBPropertyKnowledge()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyBId, conversationBId, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(companyBId, context!.TenantId);

        var knowledgeContent = string.Join("\n", context.ApprovedKnowledgeItems.Select(item => item.Content));
        Assert.Contains(CompanyBCheckIn, knowledgeContent);
        Assert.DoesNotContain(CompanyACheckIn, knowledgeContent);
        Assert.DoesNotContain(CompanyASecondPropertyCheckIn, knowledgeContent);
    }

    [Fact]
    public async Task BuildAsync_DoesNotLeakKnowledgeBetweenPropertiesOfTheSameCompany()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyAId, conversationA2Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(propertyA2Id, context!.PropertyId);

        var knowledgeContent = string.Join("\n", context.ApprovedKnowledgeItems.Select(item => item.Content));
        Assert.Contains(CompanyASecondPropertyCheckIn, knowledgeContent);
        Assert.DoesNotContain(CompanyACheckIn, knowledgeContent);
    }

    [Fact]
    public async Task BuildAsync_ExcludesUnapprovedAndInactiveKnowledgeFromGrounding()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyAId, conversationA1Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.All(context!.ApprovedKnowledgeItems, item => Assert.True(item.IsApproved));
        Assert.DoesNotContain(context.ApprovedKnowledgeItems, item => item.Title == "Unapproved Draft");
        Assert.DoesNotContain(context.ApprovedKnowledgeItems, item => item.Title == "Retired Policy");
    }

    [Fact]
    public async Task BuildAsync_KnowledgeSourcesNeverReferenceAnotherTenantsArticles()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyAId, conversationA1Id, CancellationToken.None);
        var foreignArticleIds = await dbContext.PropertyKnowledgeArticles
            .Where(item => item.CompanyId != companyAId)
            .Select(item => item.Id)
            .ToListAsync();
        var foreignSourceIds = foreignArticleIds
            .SelectMany(id => new[] { id.ToString(), id.ToString("N") })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ownArticleIds = await dbContext.PropertyKnowledgeArticles
            .Where(item => item.CompanyId == companyAId && item.PropertyId == propertyA1Id && item.IsApproved && item.IsActive)
            .Select(item => item.Id)
            .ToListAsync();
        var ownSourceIds = ownArticleIds
            .SelectMany(id => new[] { id.ToString(), id.ToString("N") })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.NotNull(context);
        Assert.NotEmpty(foreignSourceIds);
        var knowledgeSourceIds = context!.ApprovedKnowledgeItems.Select(item => item.SourceId).ToList();
        Assert.NotEmpty(knowledgeSourceIds);
        Assert.All(knowledgeSourceIds, sourceId => Assert.Contains(sourceId, ownSourceIds));
        Assert.DoesNotContain(context.Sources, source => source.SourceId is not null && foreignSourceIds.Contains(source.SourceId));
    }

    [Fact]
    public async Task BuildAsync_ConversationHistoryNeverIncludesAnotherTenantsMessages()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyAId, conversationA1Id, CancellationToken.None);

        Assert.NotNull(context);
        var messageIds = await dbContext.ConversationMessages
            .Where(message => message.ConversationId == conversationA1Id)
            .Select(message => message.Id)
            .ToListAsync();

        Assert.All(context!.VisibleMessages, message => Assert.Contains(messageIds, id => id.ToString("N") == message.MessageId || id.ToString() == message.MessageId));
    }

    [Fact]
    public async Task BuildAsync_WithConversationFromAnotherCompany_ReturnsNull()
    {
        await using var dbContext = new ApplicationDbContext(options);

        Assert.Null(await CreateBuilder(dbContext).BuildAsync(companyAId, conversationBId, CancellationToken.None));
        Assert.Null(await CreateBuilder(dbContext).BuildAsync(companyBId, conversationA1Id, CancellationToken.None));
    }

    [Fact]
    public async Task BuildAsync_WithUnknownConversationId_ReturnsNull()
    {
        await using var dbContext = new ApplicationDbContext(options);

        Assert.Null(await CreateBuilder(dbContext).BuildAsync(companyAId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task BuildAsync_WithLinkedReservation_ExposesTenantDerivedReservationContext()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyAId, conversationA1Id, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Equal(reservationAId, context!.ReservationId);
        Assert.Equal("AAA-111", context.ConfirmationNumber);
        Assert.Equal(ReservationStatus.Confirmed.ToString(), context.ReservationStatus);
        Assert.NotNull(context.CheckInDate);
        Assert.NotNull(context.CheckOutDate);
        Assert.DoesNotContain(ConversationContextWarning.MissingReservation, context.Warnings);
    }

    [Fact]
    public async Task BuildAsync_WithoutReservation_WarnsAndFabricatesNothing()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var context = await CreateBuilder(dbContext).BuildAsync(companyAId, conversationWithoutReservationId, CancellationToken.None);

        Assert.NotNull(context);
        Assert.Null(context!.ReservationId);
        Assert.Null(context.ConfirmationNumber);
        Assert.Null(context.CheckInDate);
        Assert.Null(context.CheckOutDate);
        Assert.Null(context.ReservationStatus);
        Assert.Contains(ConversationContextWarning.MissingReservation, context.Warnings);
    }

    [Fact]
    public async Task GetApprovedActiveForPropertyAsync_WithCrossTenantPropertyId_ReturnsNothing()
    {
        await using var dbContext = new ApplicationDbContext(options);
        var repository = new PropertyKnowledgeRepository(dbContext);

        var crossTenant = await repository.GetApprovedActiveForPropertyAsync(companyAId, propertyBId, CancellationToken.None);
        var ownTenant = await repository.GetApprovedActiveForPropertyAsync(companyAId, propertyA1Id, CancellationToken.None);

        Assert.Empty(crossTenant);
        Assert.NotEmpty(ownTenant);
    }

    private static ConversationContextBuilder CreateBuilder(ApplicationDbContext dbContext)
    {
        return new ConversationContextBuilder(
            new ConversationRepository(dbContext),
            new PropertyKnowledgeRepository(dbContext),
            Options.Create(new ConversationContextLimits()),
            NullLogger<ConversationContextBuilder>.Instance);
    }

    private static Company NewCompany(Guid id, string name, string slug) => new()
    {
        Id = id,
        Name = name,
        Slug = slug,
        NormalizedSlug = slug.ToUpperInvariant(),
        Status = "Active",
        Email = $"{slug}@stayflow.test",
        PhoneNumber = "+254700000001",
        CountryCode = "KE",
        TimeZone = "Africa/Nairobi",
        IsActive = true
    };

    private static Property NewProperty(Guid id, Guid companyId, string name) => new()
    {
        Id = id,
        CompanyId = companyId,
        Name = name,
        City = "Nairobi",
        CountryCode = "KE",
        AddressLine1 = "1 Demo Street",
        TimeZone = "Africa/Nairobi",
        IsActive = true
    };

    private static Guest NewGuest(Guid id, Guid companyId, string firstName, string lastName) => new()
    {
        Id = id,
        CompanyId = companyId,
        FirstName = firstName,
        LastName = lastName,
        Email = $"{firstName.ToLowerInvariant()}@stayflow.test",
        PreferredLanguage = "en",
        CountryCode = "KE",
        IsActive = true
    };

    private static Reservation NewReservation(Guid id, Guid companyId, Guid propertyId, Guid guestId, string confirmation) => new()
    {
        Id = id,
        CompanyId = companyId,
        PropertyId = propertyId,
        PrimaryGuestId = guestId,
        ConfirmationNumber = confirmation,
        CheckInDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        CheckOutDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
        Status = ReservationStatus.Confirmed,
        IsActive = true
    };

    private static Conversation NewConversation(Guid id, Guid companyId, Guid guestId, Guid propertyId, Guid? reservationId) => new()
    {
        Id = id,
        CompanyId = companyId,
        GuestId = guestId,
        PropertyId = propertyId,
        ReservationId = reservationId,
        Channel = GuestChannel.Web,
        Status = ConversationStatus.Open,
        Subject = "Check-in question",
        StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        LastActivityAt = DateTimeOffset.UtcNow
    };

    private static ConversationMessage NewGuestMessage(Guid companyId, Guid conversationId, string content) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        ConversationId = conversationId,
        SenderType = ConversationSenderType.Guest,
        MessageType = ConversationMessageType.Text,
        Content = content,
        SentAt = DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    private static PropertyKnowledgeArticle NewKnowledge(
        Guid companyId,
        Guid propertyId,
        string title,
        string content,
        bool isApproved = true,
        bool isActive = true) => new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PropertyId = propertyId,
            Title = title,
            Content = content,
            Category = PropertyKnowledgeCategory.CheckIn,
            IsApproved = isApproved,
            IsActive = isActive,
            UpdatedAt = DateTimeOffset.UtcNow
        };
}
