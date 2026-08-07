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
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class OnboardingServiceTests
{
    [Fact]
    public async Task StartAsync_CreatesResumableProgress()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.Service.StartAsync(CancellationToken.None);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Welcome", response.Data!.CurrentStep);
        Assert.NotEqual(default, response.Data.StartedAtUtc);
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

    private static async Task<Fixture> CreateFixtureAsync(string environmentName = "Development")
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

        await dbContext.SaveChangesAsync();

        var propertyService = new FakePropertyService(dbContext, companyId);
        var invitationService = new FakeInvitationService();
        var knowledgeService = new FakePropertyKnowledgeService();
        var whatsAppService = new FakeWhatsAppTemplateService();
        var entitlementService = new FakeSubscriptionEntitlementService(companyId);

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

        return new Fixture(service, dbContext, companyId);
    }

    private sealed record Fixture(
        OnboardingService Service,
        ApplicationDbContext DbContext,
        Guid CompanyId);

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

    private sealed class FakeSubscriptionEntitlementService(Guid companyId) : ISubscriptionEntitlementService
    {
        public Task<SubscriptionSnapshot> GetCurrentSnapshotAsync(Guid companyIdArg, CancellationToken cancellationToken)
            => Task.FromResult(new SubscriptionSnapshot(
                companyIdArg,
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Growth",
                "Growth",
                "Active",
                false,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(29),
                [],
                []));

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
