using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StayFlow.Api.Common;
using StayFlow.Api.Data;
using StayFlow.Api.DTOs.Conversations;
using StayFlow.Api.DTOs.Onboarding;
using StayFlow.Api.DTOs.Organizations;
using StayFlow.Api.DTOs.PropertyKnowledge;
using StayFlow.Api.DTOs.Properties;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class OnboardingServiceTests
{
    [Fact]
    public async Task GetStatusAsync_WhenNotStarted_ReturnsWelcomeAtZeroPercent()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.Service.GetStatusAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Welcome", response.Data!.CurrentStep);
        Assert.Equal("NotStarted", response.Data.CurrentStepState);
        Assert.Equal(0, response.Data.PercentComplete);
        Assert.Empty(response.Data.CompletedSteps);
        Assert.Empty(response.Data.SkippedSteps);
    }

    [Fact]
    public async Task StartAsync_CreatesResumableProgress()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.Service.StartAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("OrganizationProfile", response.Data!.CurrentStep);
        Assert.Contains("Welcome", response.Data.CompletedSteps);
        Assert.True(response.Data.PercentComplete > 0);
        Assert.NotEqual(default, response.Data.StartedAtUtc);

        var progress = await fixture.DbContext.OnboardingProgressRecords
            .SingleAsync(item => item.CompanyId == fixture.CompanyId);
        Assert.Equal("OrganizationProfile", progress.CurrentStep);
        Assert.Contains("Welcome", progress.CompletedStepsCsv);
    }

    [Fact]
    public async Task StartAsync_IsIdempotent_DoesNotResetExistingProgress()
    {
        var fixture = await CreateFixtureAsync();

        var first = await fixture.Service.StartAsync(CancellationToken.None);
        Assert.True(first.Success);

        await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow",
            Slug = "stayflow"
        }, CancellationToken.None);

        var second = await fixture.Service.StartAsync(CancellationToken.None);

        Assert.True(second.Success);
        Assert.NotNull(second.Data);
        Assert.Equal("PlanConfirmation", second.Data!.CurrentStep);
        Assert.Contains("Welcome", second.Data.CompletedSteps);
        Assert.Contains("OrganizationProfile", second.Data.CompletedSteps);
    }

    [Fact]
    public async Task StartAsync_DoesNotRegressCompletedOnboarding()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.StartAsync(CancellationToken.None);

        var progress = await fixture.DbContext.OnboardingProgressRecords
            .SingleAsync(item => item.CompanyId == fixture.CompanyId);
        progress.IsCompleted = true;
        progress.CurrentStep = OnboardingStep.Completed.ToStorageValue();
        progress.CompletedAtUtc = DateTimeOffset.UtcNow;
        progress.CompletedByUserId = progress.UserId;
        await fixture.DbContext.SaveChangesAsync();

        var restarted = await fixture.Service.StartAsync(CancellationToken.None);

        Assert.True(restarted.Success);
        Assert.NotNull(restarted.Data);
        Assert.True(restarted.Data!.IsCompleted);
        Assert.Equal("Completed", restarted.Data.CurrentStep);
    }

    [Fact]
    public async Task ResourceExistenceAlone_DoesNotMarkOnboardingCompleted()
    {
        var fixture = await CreateFixtureAsync();
        var propertyId = Guid.NewGuid();

        await fixture.DbContext.Properties.AddAsync(new Property
        {
            Id = propertyId,
            CompanyId = fixture.CompanyId,
            Name = "Existing Property",
            AddressLine1 = "Demo Street",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        }, CancellationToken.None);

        await fixture.DbContext.OnboardingProgressRecords.AddAsync(new OnboardingProgress
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            UserId = fixture.UserId,
            CurrentStep = OnboardingStep.Welcome.ToStorageValue(),
            StartedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow,
            IsCompleted = false,
            SelectedPlanName = null,
            FirstPropertyId = propertyId,
            Version = 1
        }, CancellationToken.None);

        await fixture.DbContext.SaveChangesAsync();

        var status = await fixture.Service.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Success);
        Assert.NotNull(status.Data);
        Assert.False(status.Data!.IsCompleted);
        Assert.Equal("Welcome", status.Data.CurrentStep);
        Assert.DoesNotContain("PlanConfirmation", status.Data.CompletedSteps);
        Assert.DoesNotContain("FirstProperty", status.Data.CompletedSteps);
    }

    [Fact]
    public async Task CompleteOrganizationStepAsync_UpdatesTenantProfile()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.StartAsync(CancellationToken.None);

        var response = await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow Updated",
            Slug = "stayflow-updated",
            SupportContactEmail = "support@stayflow.test",
            TimeZone = "Africa/Nairobi",
            Locale = "en"
        }, CancellationToken.None);

        Assert.True(response.Success);
        var company = await fixture.DbContext.Companies.FirstAsync(item => item.Id == fixture.CompanyId);
        Assert.Equal("StayFlow Updated", company.Name);
        Assert.Equal("stayflow-updated", company.Slug);
    }

    [Fact]
    public async Task CompleteStep_OnlyMarksTargetedStep()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.StartAsync(CancellationToken.None);

        var organizationResponse = await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow",
            Slug = "stayflow"
        }, CancellationToken.None);

        Assert.True(organizationResponse.Success);
        Assert.Contains("Welcome", organizationResponse.Data!.CompletedSteps);
        Assert.Contains("OrganizationProfile", organizationResponse.Data.CompletedSteps);
        Assert.DoesNotContain("PlanConfirmation", organizationResponse.Data.CompletedSteps);
        Assert.DoesNotContain("FirstProperty", organizationResponse.Data.CompletedSteps);

        var planResponse = await fixture.Service.CompletePlanStepAsync(new OnboardingPlanRequest { PlanName = "Growth" }, CancellationToken.None);
        Assert.True(planResponse.Success);
        Assert.Contains("PlanConfirmation", planResponse.Data!.CompletedSteps);
        Assert.DoesNotContain("FirstProperty", planResponse.Data.CompletedSteps);
    }

    [Fact]
    public async Task CompletePlanStepAsync_UsesTrustedSubscriptionState()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.StartAsync(CancellationToken.None);
        await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow",
            Slug = "stayflow"
        }, CancellationToken.None);

        var response = await fixture.Service.CompletePlanStepAsync(new OnboardingPlanRequest
        {
            PlanName = "Growth"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Growth", response.Data!.SelectedPlanName);
    }

    [Fact]
    public async Task CompletePlanStepAsync_ResolvesTrustedFreePlanWithoutPaidSubscription()
    {
        var fixture = await CreateFixtureAsync(includeActiveSubscription: false);
        await fixture.Service.StartAsync(CancellationToken.None);
        await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow",
            Slug = "stayflow"
        }, CancellationToken.None);

        var response = await fixture.Service.CompletePlanStepAsync(new OnboardingPlanRequest
        {
            PlanName = "Free"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Free", response.Data!.SelectedPlanName);
        Assert.Contains("PlanConfirmation", response.Data.CompletedSteps);
    }

    [Fact]
    public async Task CompletePlanStepAsync_WithoutPaidSubscription_RemainsFreeInProduction()
    {
        var fixture = await CreateFixtureAsync(environmentName: "Production", includeActiveSubscription: false);
        await fixture.Service.StartAsync(CancellationToken.None);
        await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow",
            Slug = "stayflow"
        }, CancellationToken.None);

        var response = await fixture.Service.CompletePlanStepAsync(new OnboardingPlanRequest
        {
            PlanName = "Free"
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Free", response.Data!.SelectedPlanName);
    }

    [Fact]
    public async Task CompletePlanStepAsync_RejectsDirectPaidPlanAssignmentOutsideTrustedState()
    {
        var fixture = await CreateFixtureAsync(includeActiveSubscription: false);
        await fixture.Service.StartAsync(CancellationToken.None);
        await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow",
            Slug = "stayflow"
        }, CancellationToken.None);

        var response = await fixture.Service.CompletePlanStepAsync(new OnboardingPlanRequest
        {
            PlanName = "Starter"
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("billing flow", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompletePropertyStepAsync_IsIdempotentForSamePayload()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToPropertyStepAsync(fixture.Service);

        var request = new OnboardingPropertyRequest
        {
            Name = "Nairobi Loft",
            AddressLine1 = "Kenyatta Avenue",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        };

        var first = await fixture.Service.CompletePropertyStepAsync(request, CancellationToken.None);
        var second = await fixture.Service.CompletePropertyStepAsync(request, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        var properties = await fixture.DbContext.Properties.Where(item => item.CompanyId == fixture.CompanyId && !item.IsDeleted).ToListAsync();
        Assert.Single(properties);
    }

    [Fact]
    public async Task SkipStepAsync_RejectsRequiredStep()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.StartAsync(CancellationToken.None);

        var response = await fixture.Service.SkipStepAsync("PlanConfirmation", new OnboardingSkipStepRequest { Reason = "no" }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("cannot be skipped", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SkipStepAsync_AllowsOptionalStep()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToTeamStepAsync(fixture.Service);

        var response = await fixture.Service.SkipStepAsync("TeamInvitations", new OnboardingSkipStepRequest { Reason = "later" }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("TeamInvitations", response.Data!.SkippedSteps);
    }

    [Fact]
    public async Task CompleteOnboardingAsync_BlockedWhenChecklistIncomplete()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.StartAsync(CancellationToken.None);

        var response = await fixture.Service.CompleteOnboardingAsync(new OnboardingCompleteRequest
        {
            ConfirmChecklistReviewed = true
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("blocked", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompleteKnowledgeStepAsync_RejectsPropertyBelongingToAnotherTenant()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToKnowledgeStepAsync(fixture.Service);

        var otherCompanyDbContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"onboarding-service-knowledge-{Guid.NewGuid():N}")
                .Options,
            tenantContext: null);

        var otherCompanyId = Guid.NewGuid();
        otherCompanyDbContext.Companies.Add(new Company
        {
            Id = otherCompanyId,
            Name = "Other Tenant",
            Slug = "other-tenant",
            NormalizedSlug = "OTHER-TENANT",
            Status = "Active",
            OwnerUserId = Guid.NewGuid(),
            Email = "other@stayflow.test",
            PhoneNumber = "+254700000003",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        otherCompanyDbContext.Properties.Add(new Property
        {
            Id = Guid.NewGuid(),
            CompanyId = otherCompanyId,
            Name = "Other Tenant Property",
            AddressLine1 = "Other Street",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        await otherCompanyDbContext.SaveChangesAsync();

        var progress = await fixture.DbContext.OnboardingProgressRecords.SingleAsync(item => item.CompanyId == fixture.CompanyId, CancellationToken.None);
        progress.FirstPropertyId = otherCompanyDbContext.Properties.Single().Id;
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.CompleteKnowledgeStepAsync(new OnboardingKnowledgeRequest
        {
            PropertyId = otherCompanyDbContext.Properties.Single().Id,
            Title = "House Rules",
            Content = "Quiet hours after 10 PM.",
            Tags = ["house-rules"]
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("does not belong to this tenant", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await fixture.DbContext.PropertyKnowledgeArticles.AnyAsync(item => item.CompanyId == fixture.CompanyId && !item.IsDeleted, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteKnowledgeStepAsync_RollsBackKnowledgeCreationWhenApprovalFails()
    {
        var fixture = await CreateFixtureAsync();
        var service = new OnboardingService(
            fixture.DbContext,
            new FakeTenantContext(fixture.CompanyId, fixture.UserId, true),
            new FakePropertyService(fixture.DbContext, fixture.CompanyId),
            new FakeInvitationService(),
            new ThrowingPropertyKnowledgeService(fixture.DbContext, fixture.CompanyId, fixture.UserId),
            new FakeWhatsAppTemplateService(),
            new FakeSubscriptionEntitlementService("Growth"),
            Options.Create(new AIProviderOptions { Provider = "Development" }),
            Options.Create(new OpenAIOptions { Model = "gpt-5.1-mini" }),
            new FakeHostEnvironment("Development"));

        await PromoteToKnowledgeStepAsync(service);

        var response = await service.CompleteKnowledgeStepAsync(new OnboardingKnowledgeRequest
        {
            Title = "House Rules",
            Content = "Quiet hours after 10 PM.",
            Tags = ["house-rules"]
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("approval failed", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await fixture.DbContext.PropertyKnowledgeArticles.AnyAsync(item => item.CompanyId == fixture.CompanyId && !item.IsDeleted, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteKnowledgeStepAsync_WithActiveSecondOrganization_CreatesKnowledgeOnlyForActiveTenant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var companyAId = Guid.NewGuid();
        var companyBId = Guid.NewGuid();
        var companyAUserId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var propertyAId = Guid.NewGuid();
        var propertyBId = Guid.NewGuid();

        var seedOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var seedContext = new ApplicationDbContext(seedOptions))
        {
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.Companies.AddRange(
                new Company
                {
                    Id = companyAId,
                    Name = "Org A",
                    Slug = "org-a",
                    NormalizedSlug = "ORG-A",
                    Status = "Active",
                    Email = "orga@stayflow.test",
                    PhoneNumber = "+254700000001",
                    CountryCode = "KE",
                    TimeZone = "Africa/Nairobi",
                    IsActive = true,
                    OnboardingState = OnboardingStep.Completed.ToStorageValue()
                },
                new Company
                {
                    Id = companyBId,
                    Name = "Org B",
                    Slug = "org-b",
                    NormalizedSlug = "ORG-B",
                    Status = "Active",
                    Email = "orgb@stayflow.test",
                    PhoneNumber = "+254700000002",
                    CountryCode = "KE",
                    TimeZone = "Africa/Nairobi",
                    IsActive = true,
                    OnboardingState = OnboardingStep.KnowledgeBaseSetup.ToStorageValue()
                });

            await seedContext.SaveChangesAsync();

            seedContext.Users.AddRange(new User
            {
                Id = companyAUserId,
                CompanyId = companyAId,
                FullName = "Org A Owner",
                Email = "orga-owner@stayflow.test",
                PhoneNumber = "+254700000003",
                Role = "Owner",
                PasswordHash = "hash",
                IsActive = true,
                PreferredLanguage = "en",
                TimeZone = "Africa/Nairobi"
            },
            new User
            {
                Id = userId,
                CompanyId = companyBId,
                FullName = "Owner",
                Email = "owner@stayflow.test",
                PhoneNumber = "+254700000004",
                Role = "Owner",
                PasswordHash = "hash",
                IsActive = true,
                PreferredLanguage = "en",
                TimeZone = "Africa/Nairobi"
            });

            seedContext.OrganizationMembers.AddRange(
                new OrganizationMember
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyAId,
                    UserId = companyAUserId,
                    Role = OrganizationRole.Owner.ToStorageValue(),
                    Status = OrganizationMemberStatus.Active.ToStorageValue(),
                    JoinedAt = DateTimeOffset.UtcNow.AddDays(-10)
                },
                new OrganizationMember
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyBId,
                    UserId = userId,
                    Role = OrganizationRole.Owner.ToStorageValue(),
                    Status = OrganizationMemberStatus.Active.ToStorageValue(),
                    JoinedAt = DateTimeOffset.UtcNow.AddDays(-1)
                });

            seedContext.Properties.AddRange(
                new Property
                {
                    Id = propertyAId,
                    CompanyId = companyAId,
                    Name = "Property A",
                    AddressLine1 = "A Street",
                    City = "Nairobi",
                    CountryCode = "KE",
                    TimeZone = "Africa/Nairobi",
                    IsActive = true
                },
                new Property
                {
                    Id = propertyBId,
                    CompanyId = companyBId,
                    Name = "Property B",
                    AddressLine1 = "B Street",
                    City = "Nairobi",
                    CountryCode = "KE",
                    TimeZone = "Africa/Nairobi",
                    IsActive = true
                });

            seedContext.OnboardingProgressRecords.AddRange(
                new OnboardingProgress
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyAId,
                    UserId = companyAUserId,
                    CurrentStep = OnboardingStep.Completed.ToStorageValue(),
                    FirstPropertyId = propertyAId,
                    CompletedStepsCsv = "Welcome,OrganizationProfile,PlanConfirmation,FirstProperty,TeamInvitations,WhatsAppSetup,AiProviderSetup,KnowledgeBaseSetup,Review,Completed",
                    SkippedStepsCsv = "DemoData",
                    StartedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
                    LastUpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-9),
                    IsCompleted = true,
                    CompletedAtUtc = DateTimeOffset.UtcNow.AddDays(-9),
                    CompletedByUserId = companyAUserId,
                    Version = 5
                },
                new OnboardingProgress
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyBId,
                    UserId = userId,
                    CurrentStep = OnboardingStep.KnowledgeBaseSetup.ToStorageValue(),
                    FirstPropertyId = propertyBId,
                    CompletedStepsCsv = "Welcome,OrganizationProfile,PlanConfirmation,FirstProperty,TeamInvitations,AiProviderSetup",
                    SkippedStepsCsv = "WhatsAppSetup",
                    SelectedPlanName = "Free",
                    StartedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    LastUpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                    IsCompleted = false,
                    Version = 3
                });

            seedContext.PropertyKnowledgeArticles.Add(new PropertyKnowledgeArticle
            {
                Id = Guid.NewGuid(),
                CompanyId = companyAId,
                PropertyId = propertyAId,
                Category = PropertyKnowledgeCategory.Other,
                Title = "Existing Org A Article",
                Content = "Org A content",
                Tags = "org-a",
                IsApproved = true,
                IsActive = true,
                CreatedByUserId = companyAUserId,
                UpdatedByUserId = companyAUserId,
                ApprovedAt = DateTimeOffset.UtcNow.AddDays(-2)
            });

            await seedContext.SaveChangesAsync();

            var companyA = await seedContext.Companies.SingleAsync(item => item.Id == companyAId);
            var companyB = await seedContext.Companies.SingleAsync(item => item.Id == companyBId);
            companyA.OwnerUserId = companyAUserId;
            companyB.OwnerUserId = userId;

            await seedContext.SaveChangesAsync();
        }

        var tenantContext = new FakeTenantContext(companyBId, userId, true);
        var runtimeOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(runtimeOptions, tenantContext);
        var propertyKnowledgeService = new PropertyKnowledgeService(
            new PropertyKnowledgeRepository(dbContext),
            tenantContext);
        var onboardingService = new OnboardingService(
            dbContext,
            tenantContext,
            new FakePropertyService(dbContext, companyBId),
            new FakeInvitationService(),
            propertyKnowledgeService,
            new FakeWhatsAppTemplateService(),
            new FakeSubscriptionEntitlementService("Free"),
            Options.Create(new AIProviderOptions { Provider = "Development" }),
            Options.Create(new OpenAIOptions { Model = "gpt-5.1-mini" }),
            new FakeHostEnvironment("Development"));

        var response = await onboardingService.CompleteKnowledgeStepAsync(new OnboardingKnowledgeRequest
        {
            Title = "House Rules",
            Content = "Quiet hours after 10 PM. Please avoid loud music. No smoking in the apartment.",
            Tags = ["house-rules"]
        }, CancellationToken.None);

        Assert.True(
            response.Success,
            $"Expected knowledge completion to succeed but failed. Message='{response.Message}', Errors='[{string.Join(", ", response.Errors)}]', CorrelationId='{response.CorrelationId}'.");
        Assert.NotNull(response.Data);
        Assert.Contains("KnowledgeBaseSetup", response.Data!.CompletedSteps);

        var orgBKnowledge = await dbContext.PropertyKnowledgeArticles
            .Where(item => item.CompanyId == companyBId && !item.IsDeleted)
            .OrderBy(item => item.Title)
            .ToListAsync();
        var created = Assert.Single(orgBKnowledge);
        Assert.Equal(propertyBId, created.PropertyId);
        Assert.Equal("House Rules", created.Title);
        Assert.True(created.IsApproved);

        Assert.Equal(1, await dbContext.PropertyKnowledgeArticles.CountAsync(item => item.CompanyId == companyAId && !item.IsDeleted));

        var orgBProgress = await dbContext.OnboardingProgressRecords.SingleAsync(item => item.CompanyId == companyBId && item.UserId == userId);
        Assert.Contains("KnowledgeBaseSetup", orgBProgress.CompletedStepsCsv);
        Assert.Equal(OnboardingStep.DemoData.ToStorageValue(), orgBProgress.CurrentStep);

        var orgAProgress = await dbContext.OnboardingProgressRecords.SingleAsync(item => item.CompanyId == companyAId && item.UserId == userId);
        Assert.Equal(OnboardingStep.Completed.ToStorageValue(), orgAProgress.CurrentStep);
        Assert.True(orgAProgress.IsCompleted);
    }

    [Fact]
    public async Task CompleteDemoDataStepAsync_RejectsPropertyBelongingToAnotherTenant()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToDemoStepAsync(fixture.Service);

        var otherCompanyDbContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"onboarding-service-tenant-{Guid.NewGuid():N}")
                .Options,
            tenantContext: null);

        var otherCompanyId = Guid.NewGuid();
        otherCompanyDbContext.Companies.Add(new Company
        {
            Id = otherCompanyId,
            Name = "Other Tenant",
            Slug = "other-tenant",
            NormalizedSlug = "OTHER-TENANT",
            Status = "Active",
            OwnerUserId = Guid.NewGuid(),
            Email = "other@stayflow.test",
            PhoneNumber = "+254700000003",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        otherCompanyDbContext.Properties.Add(new Property
        {
            Id = Guid.NewGuid(),
            CompanyId = otherCompanyId,
            Name = "Other Tenant Property",
            AddressLine1 = "Other Street",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });
        await otherCompanyDbContext.SaveChangesAsync();

        var progress = await fixture.DbContext.OnboardingProgressRecords.SingleAsync(item => item.CompanyId == fixture.CompanyId, CancellationToken.None);
        progress.FirstPropertyId = otherCompanyDbContext.Properties.Single().Id;
        await fixture.DbContext.SaveChangesAsync();

        var response = await fixture.Service.CompleteDemoDataStepAsync(new OnboardingDemoDataRequest
        {
            CreateSampleKnowledge = true,
            CreateSampleReservation = true,
            CreateSampleConversation = true,
            CreateSampleHostCopilotItem = true
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("property", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(await fixture.DbContext.Guests.AnyAsync(item => item.CompanyId == fixture.CompanyId, CancellationToken.None));
        Assert.False(await fixture.DbContext.Reservations.AnyAsync(item => item.CompanyId == fixture.CompanyId, CancellationToken.None));
        Assert.False(await fixture.DbContext.Conversations.AnyAsync(item => item.CompanyId == fixture.CompanyId, CancellationToken.None));
        Assert.False(await fixture.DbContext.PropertyKnowledgeArticles.AnyAsync(item => item.CompanyId == fixture.CompanyId && !item.IsDeleted, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteOnboardingAsync_MarksReviewCompleteAndCompletesOnboarding()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToDemoStepAsync(fixture.Service);
        await fixture.Service.SkipStepAsync("DemoData", new OnboardingSkipStepRequest { Reason = "later" }, CancellationToken.None);

        var response = await fixture.Service.CompleteOnboardingAsync(new OnboardingCompleteRequest
        {
            ConfirmChecklistReviewed = true
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.True(response.Data!.IsCompleted);
        Assert.Equal("Completed", response.Data.CurrentStep);
        Assert.Equal(100, response.Data.PercentComplete);
        Assert.Contains("Review", response.Data.CompletedSteps);
    }

    [Fact]
    public async Task ReviewSummary_ReflectsPersistedOnboardingInputs()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToPropertyStepAsync(fixture.Service);

        await fixture.Service.CompletePropertyStepAsync(new OnboardingPropertyRequest
        {
            Name = "Review Loft",
            AddressLine1 = "Lenana Road",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        }, CancellationToken.None);

        var response = await fixture.Service.GetStatusAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("StayFlow", response.Data!.ReviewSummary.OrganizationName);
        Assert.Equal("Growth", response.Data.ReviewSummary.SelectedPlanName);
        Assert.Equal("Review Loft", response.Data.ReviewSummary.FirstPropertyName);
        Assert.Equal("Development", response.Data.ReviewSummary.AiProvider);
    }

    [Fact]
    public async Task CompleteDemoDataStepAsync_BlockedInProduction()
    {
        var fixture = await CreateFixtureAsync(environmentName: "Production");
        await PromoteToDemoStepAsync(fixture.Service);

        var response = await fixture.Service.CompleteDemoDataStepAsync(new OnboardingDemoDataRequest
        {
            CreateSampleConversation = true,
            CreateSampleKnowledge = true,
            CreateSampleReservation = true,
            CreateSampleHostCopilotItem = true
        }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("blocked in production", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetAsync_ClearsWorkflowState()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToTeamStepAsync(fixture.Service);
        await fixture.Service.SkipStepAsync("TeamInvitations", new OnboardingSkipStepRequest { Reason = "later" }, CancellationToken.None);

        var response = await fixture.Service.ResetAsync(new OnboardingResetRequest { Confirm = true }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.Equal("Welcome", response.Data!.CurrentStep);
        Assert.Empty(response.Data.CompletedSteps);
        Assert.Empty(response.Data.SkippedSteps);
        Assert.False(response.Data.IsCompleted);
    }

    [Fact]
    public async Task ResetAsync_BlockedInProduction()
    {
        var fixture = await CreateFixtureAsync(environmentName: "Production");
        await fixture.Service.StartAsync(CancellationToken.None);

        var response = await fixture.Service.ResetAsync(new OnboardingResetRequest { Confirm = true }, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("blocked in production", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatusAsync_PreservesCurrentStepAcrossRefreshAndNewServiceInstance()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.StartAsync(CancellationToken.None);
        await fixture.Service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest
        {
            Name = "StayFlow",
            Slug = "stayflow"
        }, CancellationToken.None);

        var refreshed = await fixture.Service.GetStatusAsync(CancellationToken.None);
        Assert.True(refreshed.Success);
        Assert.Equal("PlanConfirmation", refreshed.Data!.CurrentStep);

        var resumedService = CreateService(fixture.DbContext, fixture.CompanyId, fixture.UserId);
        var resumed = await resumedService.GetStatusAsync(CancellationToken.None);
        Assert.True(resumed.Success);
        Assert.Equal("PlanConfirmation", resumed.Data!.CurrentStep);
    }

    [Fact]
    public async Task CompleteInvitationsStepAsync_ReturnsPerInviteResults()
    {
        var fixture = await CreateFixtureAsync();
        await PromoteToTeamStepAsync(fixture.Service);

        var response = await fixture.Service.CompleteInvitationsStepAsync(new OnboardingInvitationsRequest
        {
            Invitations =
            [
                new OnboardingInvitationRequest { Email = "host1@test.io", Role = "Host" },
                new OnboardingInvitationRequest { Email = "manager1@test.io", Role = "Manager" }
            ]
        }, CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.NotNull(response.Data!.Result);
        Assert.Equal(2, response.Data.Result!.Results.Count);
        Assert.All(response.Data.Result.Results, item => Assert.True(item.Success));
    }

    private static async Task PromoteToPropertyStepAsync(OnboardingService service)
    {
        await service.StartAsync(CancellationToken.None);
        await service.CompleteOrganizationStepAsync(new OnboardingOrganizationRequest { Name = "StayFlow", Slug = "stayflow" }, CancellationToken.None);
        await service.CompletePlanStepAsync(new OnboardingPlanRequest { PlanName = "Growth" }, CancellationToken.None);
    }

    private static async Task PromoteToKnowledgeStepAsync(OnboardingService service)
    {
        await PromoteToPropertyStepAsync(service);
        await service.CompletePropertyStepAsync(new OnboardingPropertyRequest
        {
            Name = "Nairobi Loft",
            AddressLine1 = "Kenyatta Avenue",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        }, CancellationToken.None);
        await service.CompleteAiProviderStepAsync(new OnboardingAiProviderRequest
        {
            AcknowledgeDeterministicFallback = true,
            SkipIfDeterministicOnly = false
        }, CancellationToken.None);
    }

    private static async Task PromoteToTeamStepAsync(OnboardingService service)
    {
        await PromoteToPropertyStepAsync(service);
        await service.CompletePropertyStepAsync(new OnboardingPropertyRequest
        {
            Name = "Nairobi Loft",
            AddressLine1 = "Kenyatta Avenue",
            City = "Nairobi",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi"
        }, CancellationToken.None);
    }

    private static async Task PromoteToDemoStepAsync(OnboardingService service)
    {
        await PromoteToTeamStepAsync(service);
        await service.SkipStepAsync("TeamInvitations", new OnboardingSkipStepRequest { Reason = "later" }, CancellationToken.None);
        await service.SkipStepAsync("WhatsAppSetup", new OnboardingSkipStepRequest { Reason = "later" }, CancellationToken.None);
        await service.CompleteAiProviderStepAsync(new OnboardingAiProviderRequest
        {
            AcknowledgeDeterministicFallback = true,
            SkipIfDeterministicOnly = false
        }, CancellationToken.None);
        await service.CompleteKnowledgeStepAsync(new OnboardingKnowledgeRequest
        {
            Title = "Quiet Hours",
            Content = "No loud music after 10 PM.",
            Tags = ["house-rules"]
        }, CancellationToken.None);
    }

    private static async Task<Fixture> CreateFixtureAsync(string environmentName = "Development", bool includeActiveSubscription = true)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"onboarding-service-{Guid.NewGuid():N}")
            .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext(companyId, userId, true);
        var dbContext = new ApplicationDbContext(options, tenantContext);

        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Name = "StayFlow",
            Slug = "stayflow",
            NormalizedSlug = "STAYFLOW",
            Status = "Active",
            OwnerUserId = userId,
            Email = "owner@stayflow.test",
            PhoneNumber = "+254700000001",
            CountryCode = "KE",
            TimeZone = "Africa/Nairobi",
            IsActive = true
        });

        dbContext.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            FullName = "Owner",
            Email = "owner@stayflow.test",
            PhoneNumber = "+254700000002",
            Role = "Owner",
            PasswordHash = "hash",
            IsActive = true
        });

        dbContext.OrganizationMembers.Add(new OrganizationMember
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Role = OrganizationRole.Owner.ToStorageValue(),
            Status = OrganizationMemberStatus.Active.ToStorageValue(),
            JoinedAt = DateTimeOffset.UtcNow.AddDays(-3)
        });

        if (includeActiveSubscription)
        {
            dbContext.TenantSubscriptions.Add(new TenantSubscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SubscriptionPlanId = Guid.NewGuid(),
                Status = SubscriptionStatus.Active.ToStorageValue(),
                CurrentPeriodStartUtc = DateTimeOffset.UtcNow.AddDays(-1),
                CurrentPeriodEndUtc = DateTimeOffset.UtcNow.AddDays(29),
                ExternalSubscriptionId = "sub_123",
                ExternalPriceId = "price_growth"
            });
        }

        await dbContext.SaveChangesAsync();

        var propertyService = new FakePropertyService(dbContext, companyId);
        var invitationService = new FakeInvitationService();
        var knowledgeService = new FakePropertyKnowledgeService();
        var whatsAppService = new FakeWhatsAppTemplateService();
        var entitlementService = new FakeSubscriptionEntitlementService(includeActiveSubscription ? "Growth" : null);

        var service = new OnboardingService(
            dbContext,
            tenantContext,
            propertyService,
            invitationService,
            knowledgeService,
            whatsAppService,
            entitlementService,
            Options.Create(new AIProviderOptions { Provider = "Development" }),
            Options.Create(new OpenAIOptions { Model = "gpt-5.1-mini" }),
            new FakeHostEnvironment(environmentName));

        return new Fixture(service, dbContext, companyId, userId);
    }

    private static OnboardingService CreateService(
        ApplicationDbContext dbContext,
        Guid companyId,
        Guid userId,
        string environmentName = "Development")
    {
        var tenantContext = new FakeTenantContext(companyId, userId, true);

        return new OnboardingService(
            dbContext,
            tenantContext,
            new FakePropertyService(dbContext, companyId),
            new FakeInvitationService(),
            new FakePropertyKnowledgeService(),
            new FakeWhatsAppTemplateService(),
            new FakeSubscriptionEntitlementService("Growth"),
            Options.Create(new AIProviderOptions { Provider = "Development" }),
            Options.Create(new OpenAIOptions { Model = "gpt-5.1-mini" }),
            new FakeHostEnvironment(environmentName));
    }

    private sealed record Fixture(
        OnboardingService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId,
        Guid UserId);

    private sealed class ThrowingPropertyKnowledgeService(ApplicationDbContext dbContext, Guid companyId, Guid userId) : IPropertyKnowledgeService
    {
        public async Task<ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>> GetAsync(Guid propertyId, PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
            => ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>.Ok(new PagedResult<PropertyKnowledgeSummaryResponse>());

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> GetByIdAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public async Task<ApiResponse<PropertyKnowledgeDetailResponse>> CreateAsync(Guid propertyId, CreatePropertyKnowledgeRequest request, CancellationToken cancellationToken)
        {
            var article = new PropertyKnowledgeArticle
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PropertyId = propertyId,
                Category = request.Category,
                Title = request.Title.Trim(),
                Content = request.Content.Trim(),
                Tags = string.Join(',', request.Tags),
                Summary = request.Summary,
                IsActive = true,
                IsApproved = false,
                CreatedByUserId = userId,
                UpdatedByUserId = userId
            };

            await dbContext.PropertyKnowledgeArticles.AddAsync(article, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return ApiResponse<PropertyKnowledgeDetailResponse>.Ok(new PropertyKnowledgeDetailResponse
            {
                Id = article.Id,
                PropertyId = propertyId,
                PropertyName = "Demo Property",
                Title = article.Title,
                Content = article.Content,
                Category = article.Category,
                CategoryLabel = article.Category.ToString(),
                Summary = article.Summary,
                Tags = request.Tags,
                Priority = request.Priority,
                IsApproved = false,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CanBeUsedByAI = false
            });
        }

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> UpdateAsync(Guid propertyId, Guid knowledgeId, UpdatePropertyKnowledgeRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> ApproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromException<ApiResponse<PropertyKnowledgeDetailResponse>>(new InvalidOperationException("approval failed"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> UnapproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> ActivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> DeactivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<object>> DeleteAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<object>.Fail("not implemented"));
    }

    private sealed class FakePropertyService(ApplicationDbContext dbContext, Guid companyId) : IPropertyService
    {
        public Task<ApiResponse<PagedResult<PropertySummaryDto>>> GetAsync(PropertyQueryParameters query, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PagedResult<PropertySummaryDto>>.Ok(new PagedResult<PropertySummaryDto>()));

        public Task<ApiResponse<PropertyDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyDto>.Fail("not implemented"));

        public async Task<ApiResponse<PropertyDto>> CreateAsync(CreatePropertyRequest request, CancellationToken cancellationToken)
        {
            var existing = await dbContext.Properties.FirstOrDefaultAsync(item => item.CompanyId == companyId
                && item.Name == request.Name
                && item.AddressLine1 == request.AddressLine1
                && item.City == request.City
                && item.TimeZone == request.TimeZone
                && !item.IsDeleted, cancellationToken);

            if (existing is null)
            {
                existing = new Property
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Name = request.Name,
                    AddressLine1 = request.AddressLine1,
                    AddressLine2 = request.AddressLine2,
                    City = request.City,
                    CountryCode = request.CountryCode,
                    TimeZone = request.TimeZone,
                    Description = request.Description,
                    IsActive = true
                };

                await dbContext.Properties.AddAsync(existing, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return ApiResponse<PropertyDto>.Ok(new PropertyDto
            {
                Id = existing.Id,
                CompanyId = existing.CompanyId,
                Name = existing.Name,
                AddressLine1 = existing.AddressLine1,
                AddressLine2 = existing.AddressLine2,
                City = existing.City,
                CountryCode = existing.CountryCode,
                TimeZone = existing.TimeZone,
                Description = existing.Description,
                IsActive = true,
                CreatedAt = existing.CreatedAt,
                UpdatedAt = existing.UpdatedAt
            });
        }

        public Task<ApiResponse<PropertyDto>> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyDto>.Fail("not implemented"));

        public Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<object>.Fail("not implemented"));
    }

    private sealed class FakeInvitationService : IOrganizationInvitationService
    {
        public Task<ApiResponse<CreatedOrganizationInvitationDto>> CreateAsync(CreateOrganizationInvitationRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<CreatedOrganizationInvitationDto>.Ok(new CreatedOrganizationInvitationDto
            {
                Invitation = new OrganizationInvitationDto
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email,
                    Role = request.Role,
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
                },
                InvitationToken = "token",
                InvitationLink = "https://example.test/invite"
            }));

        public Task<ApiResponse<IReadOnlyCollection<OrganizationInvitationDto>>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<IReadOnlyCollection<OrganizationInvitationDto>>.Ok([]));

        public Task<ApiResponse<object>> RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<object>.Ok(new { invitationId }));

        public Task<ApiResponse<ResentOrganizationInvitationDto>> ResendAsync(Guid invitationId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<ResentOrganizationInvitationDto>.Fail("not implemented"));

        public Task<ApiResponse<object>> AcceptAsync(AcceptOrganizationInvitationRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<object>.Fail("not implemented"));

        public Task<ApiResponse<object>> RejectAsync(RejectOrganizationInvitationRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<object>.Fail("not implemented"));
    }

    private sealed class FakePropertyKnowledgeService : IPropertyKnowledgeService
    {
        public Task<ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>> GetAsync(Guid propertyId, PropertyKnowledgeListQuery query, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PagedResult<PropertyKnowledgeSummaryResponse>>.Ok(new PagedResult<PropertyKnowledgeSummaryResponse>()));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> GetByIdAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> CreateAsync(Guid propertyId, CreatePropertyKnowledgeRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Ok(new PropertyKnowledgeDetailResponse
            {
                Id = Guid.NewGuid(),
                PropertyId = propertyId,
                PropertyName = "Demo Property",
                Title = request.Title,
                Content = request.Content,
                Category = request.Category,
                CategoryLabel = request.Category.ToString(),
                Summary = request.Summary,
                Tags = request.Tags,
                Priority = request.Priority,
                IsApproved = false,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CanBeUsedByAI = false
            }));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> UpdateAsync(Guid propertyId, Guid knowledgeId, UpdatePropertyKnowledgeRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> ApproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Ok(new PropertyKnowledgeDetailResponse
            {
                Id = knowledgeId,
                PropertyId = propertyId,
                PropertyName = "Demo Property",
                Title = "Approved",
                Content = "Approved",
                Category = PropertyKnowledgeCategory.Other,
                CategoryLabel = "Other",
                IsApproved = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CanBeUsedByAI = true
            }));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> UnapproveAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> ActivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<PropertyKnowledgeDetailResponse>> DeactivateAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<PropertyKnowledgeDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<object>> DeleteAsync(Guid propertyId, Guid knowledgeId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<object>.Fail("not implemented"));
    }

    private sealed class FakeWhatsAppTemplateService : IWhatsAppTemplateService
    {
        public Task<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>> GetIntegrationsAsync(CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>.Ok([
                new WhatsAppIntegrationSummaryResponse
                {
                    Id = Guid.NewGuid(),
                    DisplayName = "Demo WA",
                    BusinessPhoneNumberMasked = "+1******1234",
                    IsActive = true,
                    IsProductionEnabled = false,
                    Mode = "Development",
                    HealthStatus = "Healthy"
                }
            ]));

        public Task<ApiResponse<WhatsAppIntegrationHealthResponse>> CheckHealthAsync(Guid integrationId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<WhatsAppIntegrationHealthResponse>.Ok(new WhatsAppIntegrationHealthResponse
            {
                IntegrationId = integrationId,
                Status = "Healthy",
                Message = "ok",
                IsSendCapable = true,
                CheckedAt = DateTimeOffset.UtcNow
            }));

        public Task<ApiResponse<WhatsAppTemplateSyncResponse>> SyncTemplatesAsync(Guid integrationId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<WhatsAppTemplateSyncResponse>.Fail("not implemented"));

        public Task<ApiResponse<WhatsAppTemplateListResponse>> ListTemplatesAsync(Guid integrationId, WhatsAppTemplateListQuery query, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<WhatsAppTemplateListResponse>.Fail("not implemented"));

        public Task<ApiResponse<WhatsAppTemplateDetailResponse>> GetTemplateAsync(Guid integrationId, Guid templateId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<WhatsAppTemplateDetailResponse>.Fail("not implemented"));

        public Task<ApiResponse<WhatsAppTemplatePreviewResponse>> PreviewTemplateAsync(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<WhatsAppTemplatePreviewResponse>.Fail("not implemented"));

        public Task<ApiResponse<ConversationMessageResponse>> SendTemplateMessageAsync(Guid conversationId, Guid templateId, SendWhatsAppTemplateMessageRequest request, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<ConversationMessageResponse>.Fail("not implemented"));

        public Task<ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>> GetCustomerServiceWindowStatusAsync(Guid conversationId, CancellationToken cancellationToken)
            => Task.FromResult(ApiResponse<WhatsAppCustomerServiceWindowStatusResponse>.Fail("not implemented"));
    }

    private sealed class FakeSubscriptionEntitlementService(string? planName) : ISubscriptionEntitlementService
    {
        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyIdArg, CancellationToken cancellationToken)
        {
            var resolvedPlan = string.IsNullOrWhiteSpace(planName) ? "Free" : planName;

            return Task.FromResult(new SubscriptionSnapshot(
                companyIdArg,
                Guid.NewGuid(),
                Guid.NewGuid(),
                resolvedPlan,
                resolvedPlan,
                "Active",
                false,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(29),
                [],
                []));
        }

        public Task<SubscriptionSnapshot?> TryGetCurrentSnapshotAsync(Guid companyIdArg, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(planName))
            {
                return Task.FromResult<SubscriptionSnapshot?>(null);
            }

            return Task.FromResult<SubscriptionSnapshot?>(new SubscriptionSnapshot(
                companyIdArg,
                Guid.NewGuid(),
                Guid.NewGuid(),
                planName,
                planName,
                "Active",
                false,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(29),
                [],
                []));
        }

        public Task EnsureFeatureEnabledAsync(Guid companyIdArg, string featureKey, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<UsageConsumptionResult> ConsumeQuotaAsync(Guid companyIdArg, UsageMetric metric, long quantity, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new UsageConsumptionResult(metric, null, 0, quantity, true, false));

        public Task<SubscriptionSnapshot> UpdatePlanAsync(Guid companyIdArg, Guid? planId, string? planName, string? notes, CancellationToken cancellationToken)
            => GetCurrentSnapshotAsync(companyIdArg, cancellationToken);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "StayFlow";
        public string ContentRootPath { get; set; } = "/workspace";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FakeTenantContext(Guid? companyId, Guid? userId, bool isAuthenticated) : ICurrentTenantContext, ITenantContext
    {
        public Guid? TenantId => companyId;
        public Guid? CompanyId { get; } = companyId;
        public Guid? UserId { get; } = userId;
        public string? CorrelationId => "corr-onboarding-tests";
        public bool IsAuthenticated { get; } = isAuthenticated;
    }
}
