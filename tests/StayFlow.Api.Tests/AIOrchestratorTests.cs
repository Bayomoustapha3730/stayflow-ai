using System.Text.Json;
using StayFlow.Api.DTOs.AIContext;
using StayFlow.Api.DTOs.AIPrompt;
using StayFlow.Api.DTOs.AIProvider;
using StayFlow.Api.DTOs.AIResponseValidation;
using StayFlow.Api.DTOs.AIOrchestration;
using StayFlow.Api.DTOs.ReservationContext;
using StayFlow.Api.Models;
using StayFlow.Api.Repositories;
using StayFlow.Api.Services;

namespace StayFlow.Api.Tests;

public sealed class AIOrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_ReadyContextCallsPromptBuilderProviderAndValidator()
    {
        var fixture = new Fixture();

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.Responded, result.Outcome);
        Assert.True(fixture.PromptBuilder.WasCalled);
        Assert.True(fixture.Provider.WasCalled);
        Assert.True(fixture.Validator.WasCalled);
    }

    [Fact]
    public async Task ProcessAsync_ValidProviderResponseReturnsResponded()
    {
        var fixture = new Fixture();
        fixture.Provider.Result = AIProviderResult.Success("Safe response", "Fake", "fake-model", "req-1", 10);

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.Responded, result.Outcome);
        Assert.Equal("Safe response", result.GuestSafeMessage);
        Assert.Equal("Fake", result.ProviderMetadata!.ProviderName);
    }

    [Fact]
    public async Task ProcessAsync_ClarificationRequiredDoesNotCallProvider()
    {
        var fixture = new Fixture();
        fixture.ContextBuilder.Result = new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.ClarificationRequired,
            QuestionCategories = [QuestionContextCategory.WiFi],
            CandidateLabels = [new ReservationCandidateLabel { PropertyName = "A", City = "Nairobi", CheckInDate = new DateOnly(2026, 8, 1) }]
        };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.ClarificationRequired, result.Outcome);
        Assert.False(fixture.Provider.WasCalled);
        Assert.Single(result.CandidateLabels);
    }

    [Fact]
    public async Task ProcessAsync_EscalationRequiredDoesNotCallProvider()
    {
        var fixture = new Fixture();
        fixture.ContextBuilder.Result = new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.EscalationRequired,
            QuestionCategories = [QuestionContextCategory.WiFi],
            EscalationReason = "TenantContextUnavailable"
        };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.EscalationRequired, result.Outcome);
        Assert.False(fixture.Provider.WasCalled);
    }

    [Fact]
    public async Task ProcessAsync_NoEligibleReservationWithoutUsableContextDoesNotCallProvider()
    {
        var fixture = new Fixture();
        fixture.ContextBuilder.Result = new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.NoEligibleReservation,
            QuestionCategories = [QuestionContextCategory.PropertyAccess],
            Context = new AIContext
            {
                Safety = new AISafetyContext { RequiresPropertyAccessAuthorization = true, ContextMinimized = true }
            }
        };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.NoEligibleReservation, result.Outcome);
        Assert.False(fixture.Provider.WasCalled);
    }

    [Fact]
    public async Task ProcessAsync_NoEligibleReservationWithApprovedGeneralContextMayCallProvider()
    {
        var fixture = new Fixture();
        fixture.ContextBuilder.Result = new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.NoEligibleReservation,
            QuestionCategories = [QuestionContextCategory.General],
            Context = new AIContext
            {
                Guest = new AIGuestContext { PreferredLanguage = "en" },
                Safety = new AISafetyContext { TenantValidated = true, GuestValidated = true, ContextMinimized = true }
            }
        };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.Responded, result.Outcome);
        Assert.True(fixture.Provider.WasCalled);
    }

    [Fact]
    public async Task ProcessAsync_BlockedValidationNeverReturnsProviderResponse()
    {
        var fixture = new Fixture();
        fixture.Provider.Result = AIProviderResult.Success("door code is 1234", "Fake", "fake-model", "req-1", 10);
        fixture.Validator.Result = new AIResponseValidationResult
        {
            Outcome = AIResponseValidationOutcome.Blocked,
            Violations = [AIResponseViolationCode.PropertyAccessDisclosure],
            SafeMessage = AIResponseSafeMessages.PropertyAccessVerificationRequired
        };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.Blocked, result.Outcome);
        Assert.Equal(AIResponseSafeMessages.PropertyAccessVerificationRequired, result.GuestSafeMessage);
        Assert.DoesNotContain("1234", result.GuestSafeMessage);
    }

    [Fact]
    public async Task ProcessAsync_ValidationEscalationNeverReturnsProviderResponse()
    {
        var fixture = new Fixture();
        fixture.Provider.Result = AIProviderResult.Success("very long unsafe provider response", "Fake", "fake-model", "req-1", 10);
        fixture.Validator.Result = new AIResponseValidationResult
        {
            Outcome = AIResponseValidationOutcome.EscalationRequired,
            Violations = [AIResponseViolationCode.ResponseTooLong],
            SafeMessage = AIResponseSafeMessages.ResponseUnavailable
        };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.EscalationRequired, result.Outcome);
        Assert.Equal(AIResponseSafeMessages.ResponseUnavailable, result.GuestSafeMessage);
        Assert.DoesNotContain("unsafe provider response", result.GuestSafeMessage);
    }

    [Fact]
    public async Task ProcessAsync_ProviderUnavailableReturnsSafeFallback()
    {
        var fixture = new Fixture();
        fixture.Provider.Result = new AIProviderResult { Outcome = AIProviderOutcome.Unavailable, ProviderName = "Fake", FailureCategory = "Unavailable" };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(AIOrchestrationSafeMessages.ProviderUnavailable, result.GuestSafeMessage);
    }

    [Fact]
    public async Task ProcessAsync_ProviderFailureReturnsSafeFallback()
    {
        var fixture = new Fixture();
        fixture.Provider.Result = new AIProviderResult { Outcome = AIProviderOutcome.Failed, ProviderName = "Fake", FailureCategory = "Failed" };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(AIOrchestrationSafeMessages.ProviderUnavailable, result.GuestSafeMessage);
    }

    [Fact]
    public async Task ProcessAsync_ProviderExceptionIsCaught()
    {
        var fixture = new Fixture();
        fixture.Provider.ThrowOnGenerate = true;

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(AIOrchestrationSafeMessages.ProviderUnavailable, result.GuestSafeMessage);
    }

    [Fact]
    public async Task ProcessAsync_PropertyAccessRequestNeverReturnsCredentials()
    {
        var fixture = new Fixture();
        fixture.ContextBuilder.Result = Fixture.ReadyContext([QuestionContextCategory.PropertyAccess], accessRestricted: true);
        fixture.Provider.Result = AIProviderResult.Success("Access details require verification or host assistance.", "Fake", "fake-model", "req-1", 10);

        var result = await fixture.ProcessAsync("What is the door code?");

        Assert.Equal(AIOrchestrationOutcome.Responded, result.Outcome);
        Assert.DoesNotContain("1234", result.GuestSafeMessage);
    }

    [Fact]
    public async Task ProcessAsync_TenantACannotOrchestrateUsingTenantBGuestContext()
    {
        var fixture = new Fixture();
        fixture.ContextBuilder.Result = new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.EscalationRequired,
            QuestionCategories = [QuestionContextCategory.General],
            EscalationReason = "ResolvedContextValidationFailed"
        };

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.EscalationRequired, result.Outcome);
        Assert.False(fixture.Provider.WasCalled);
    }

    [Fact]
    public async Task ProcessAsync_ResultDoesNotExposePromptSystemInstructionsOrInternalIds()
    {
        var fixture = new Fixture();
        var guestId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        fixture.ContextBuilder.Result = Fixture.ReadyContext(metadata: new AIContextBuildMetadata
        {
            CompanyId = companyId,
            GuestId = guestId,
            ReservationId = reservationId,
            PropertyId = propertyId
        });

        var result = await fixture.ProcessAsync(guestId: guestId);
        var json = JsonSerializer.Serialize(result);

        Assert.DoesNotContain("PromptPackage", json);
        Assert.DoesNotContain("SystemInstructions", json);
        Assert.DoesNotContain("CompanyId", json);
        Assert.DoesNotContain("GuestId", json);
        Assert.DoesNotContain("ReservationId", json);
        Assert.DoesNotContain("PropertyId", json);
        Assert.DoesNotContain(companyId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(guestId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(reservationId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(propertyId.ToString(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_ResolvedCompanyIdReachesValidatorProtectedIdentifiers()
    {
        var fixture = new Fixture();
        var companyId = Guid.NewGuid();
        fixture.ContextBuilder.Result = Fixture.ReadyContext(metadata: new AIContextBuildMetadata { CompanyId = companyId });

        await fixture.ProcessAsync();

        Assert.Equal(companyId, fixture.Validator.LastRequest!.ProtectedIdentifiers.CompanyId);
    }

    [Fact]
    public async Task ProcessAsync_ResolvedGuestIdReachesValidatorWhenRequestGuestIdIsNull()
    {
        var fixture = new Fixture();
        var guestId = Guid.NewGuid();
        fixture.ContextBuilder.Result = Fixture.ReadyContext(metadata: new AIContextBuildMetadata { GuestId = guestId });

        await fixture.ProcessAsync(guestId: null);

        Assert.Equal(guestId, fixture.Validator.LastRequest!.ProtectedIdentifiers.GuestId);
    }

    [Fact]
    public async Task ProcessAsync_ResolvedReservationIdReachesValidatorProtectedIdentifiers()
    {
        var fixture = new Fixture();
        var reservationId = Guid.NewGuid();
        fixture.ContextBuilder.Result = Fixture.ReadyContext(metadata: new AIContextBuildMetadata { ReservationId = reservationId });

        await fixture.ProcessAsync();

        Assert.Equal(reservationId, fixture.Validator.LastRequest!.ProtectedIdentifiers.ReservationId);
    }

    [Fact]
    public async Task ProcessAsync_ResolvedPropertyIdReachesValidatorProtectedIdentifiers()
    {
        var fixture = new Fixture();
        var propertyId = Guid.NewGuid();
        fixture.ContextBuilder.Result = Fixture.ReadyContext(metadata: new AIContextBuildMetadata { PropertyId = propertyId });

        await fixture.ProcessAsync();

        Assert.Equal(propertyId, fixture.Validator.LastRequest!.ProtectedIdentifiers.PropertyId);
    }

    [Theory]
    [InlineData("CompanyId")]
    [InlineData("GuestId")]
    [InlineData("ReservationId")]
    [InlineData("PropertyId")]
    public async Task ProcessAsync_ProviderResponseContainingProtectedIdentifierIsBlocked(string identifierName)
    {
        var identifiers = new AIContextBuildMetadata
        {
            CompanyId = Guid.NewGuid(),
            GuestId = Guid.NewGuid(),
            ReservationId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid()
        };
        var leakedIdentifier = identifierName switch
        {
            "CompanyId" => identifiers.CompanyId,
            "GuestId" => identifiers.GuestId,
            "ReservationId" => identifiers.ReservationId,
            _ => identifiers.PropertyId
        };
        var fixture = new Fixture(useRealValidator: true);
        fixture.ContextBuilder.Result = Fixture.ReadyContext(metadata: identifiers);
        fixture.Provider.Result = AIProviderResult.Success($"The internal value is {leakedIdentifier}.", "Fake", "fake-model", "req-1", 10);

        var result = await fixture.ProcessAsync();

        Assert.Equal(AIOrchestrationOutcome.Blocked, result.Outcome);
        Assert.Contains(AIResponseViolationCode.InternalIdentifierDisclosure, result.ValidationViolations);
        Assert.DoesNotContain(leakedIdentifier.ToString()!, result.GuestSafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAsync_NoEligibleReservationDoesNotInventReservationOrPropertyIdentifiers()
    {
        var fixture = new Fixture();
        fixture.ContextBuilder.Result = new AIContextBuildResult
        {
            Outcome = AIContextBuildOutcome.NoEligibleReservation,
            QuestionCategories = [QuestionContextCategory.General],
            Metadata = new AIContextBuildMetadata { CompanyId = Guid.NewGuid(), GuestId = Guid.NewGuid() },
            Context = new AIContext
            {
                Guest = new AIGuestContext { PreferredLanguage = "en" },
                Safety = new AISafetyContext { TenantValidated = true, GuestValidated = true, ContextMinimized = true }
            }
        };

        await fixture.ProcessAsync();

        Assert.Null(fixture.Validator.LastRequest!.ProtectedIdentifiers.ReservationId);
        Assert.Null(fixture.Validator.LastRequest.ProtectedIdentifiers.PropertyId);
    }

    [Fact]
    public async Task ProcessAsync_GuestMessageAndProviderResponseAreNotWrittenToOrchestrationLogs()
    {
        var fixture = new Fixture();
        fixture.Provider.Result = AIProviderResult.Success("full provider response secret", "Fake", "fake-model", "req-1", 10);

        await fixture.ProcessAsync("guest message secret");

        Assert.DoesNotContain(fixture.Repository.AuditLogs, log => log.Details?.Contains("guest message secret", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(fixture.Repository.AuditLogs, log => log.Details?.Contains("full provider response secret", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task DevelopmentAIProvider_ReturnsDeterministicWiFiResponse()
    {
        var provider = new DevelopmentAIProvider();

        var result = await provider.GenerateAsync(ProviderRequest([QuestionContextCategory.WiFi]), CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Contains("WiFi", result.ResponseText);
    }

    [Fact]
    public async Task DevelopmentAIProvider_ReturnsDeterministicParkingResponse()
    {
        var provider = new DevelopmentAIProvider();

        var result = await provider.GenerateAsync(ProviderRequest([QuestionContextCategory.Parking]), CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Contains("Parking", result.ResponseText);
    }

    [Fact]
    public async Task DevelopmentAIProvider_ReturnsSafePropertyAccessResponse()
    {
        var provider = new DevelopmentAIProvider();
        var request = ProviderRequest([QuestionContextCategory.PropertyAccess], propertyAccessRestricted: true);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.Equal(AIProviderOutcome.Success, result.Outcome);
        Assert.Contains("verification", result.ResponseText);
        Assert.DoesNotContain("1234", result.ResponseText);
    }

    private static AIProviderRequest ProviderRequest(IReadOnlyCollection<QuestionContextCategory> categories, bool propertyAccessRestricted = false)
    {
        return new AIProviderRequest
        {
            PromptPackage = new AIPromptPackage(),
            RenderedMessages = [],
            QuestionCategories = categories,
            ResponseConstraints = new AIResponseConstraints { PropertyAccessRestricted = propertyAccessRestricted }
        };
    }

    private sealed class Fixture
    {
        public Fixture(bool useRealValidator = false)
        {
            Repository = new FakeAIContextRepository();
            ContextBuilder = new FakeContextBuilder();
            PromptBuilder = new FakePromptBuilder();
            Provider = new FakeProvider();
            Validator = useRealValidator
                ? new CapturingValidator(new AIResponseValidator(Repository, new FakeCurrentTenantContext(), Microsoft.Extensions.Options.Options.Create(new AIPromptOptions())))
                : new CapturingValidator();
            Orchestrator = new AIOrchestrator(
                ContextBuilder,
                PromptBuilder,
                Provider,
                Validator,
                Repository,
                new FakeCurrentTenantContext());
        }

        public FakeAIContextRepository Repository { get; }
        public FakeContextBuilder ContextBuilder { get; }
        public FakePromptBuilder PromptBuilder { get; }
        public FakeProvider Provider { get; }
        public CapturingValidator Validator { get; }
        private AIOrchestrator Orchestrator { get; }

        public Task<AIOrchestrationResult> ProcessAsync(string message = "What is the wifi?", Guid? guestId = null)
        {
            return Orchestrator.ProcessAsync(new AIOrchestrationRequest
            {
                GuestMessage = message,
                GuestId = guestId,
                CurrentTimestamp = DateTimeOffset.UtcNow
            }, CancellationToken.None);
        }

        public static AIContextBuildResult ReadyContext(
            IReadOnlyCollection<QuestionContextCategory>? categories = null,
            bool accessRestricted = false,
            AIContextBuildMetadata? metadata = null)
        {
            return new AIContextBuildResult
            {
                Outcome = AIContextBuildOutcome.Ready,
                QuestionCategories = categories ?? [QuestionContextCategory.WiFi],
                Metadata = metadata ?? new AIContextBuildMetadata
                {
                    CompanyId = Guid.NewGuid(),
                    GuestId = Guid.NewGuid(),
                    ReservationId = Guid.NewGuid(),
                    PropertyId = Guid.NewGuid()
                },
                Context = new AIContext
                {
                    Guest = new AIGuestContext { PreferredLanguage = "en" },
                    Reservation = new AIReservationContext
                    {
                        Status = "Confirmed",
                        CheckInDate = new DateOnly(2026, 8, 1),
                        CheckOutDate = new DateOnly(2026, 8, 4),
                        CurrentStayPhase = "PreArrival",
                        Adults = 2,
                        Children = 0
                    },
                    Property = new AIPropertyContext
                    {
                        DisplayName = "Demo Stay",
                        City = "Nairobi",
                        CountryCode = "KE",
                        TimeZone = "Africa/Nairobi"
                    },
                    Safety = new AISafetyContext
                    {
                        RequiresPropertyAccessAuthorization = accessRestricted,
                        ReservationContextResolved = true,
                        TenantValidated = true,
                        GuestValidated = true,
                        ContextMinimized = true
                    }
                }
            };
        }
    }

    private sealed class FakeContextBuilder : IAIContextBuilder
    {
        public AIContextBuildResult Result { get; set; } = Fixture.ReadyContext();

        public Task<AIContextBuildResult> BuildAsync(AIContextRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class FakePromptBuilder : IAIPromptBuilder
    {
        public bool WasCalled { get; private set; }

        public AIPromptPackage Build(AIPromptBuildRequest request)
        {
            WasCalled = true;
            return new AIPromptPackage
            {
                SystemInstructions = "system secret",
                GuestMessage = request.GuestQuestion,
                PreferredLanguage = request.AIContext.Guest?.PreferredLanguage ?? "en",
                ResponseConstraints = new AIResponseConstraints { PropertyAccessRestricted = request.AIContext.Safety.RequiresPropertyAccessAuthorization },
                RenderedMessages = [new AIPromptMessage { Role = "user", Content = request.GuestQuestion }]
            };
        }
    }

    private sealed class FakeProvider : IAIProvider
    {
        public bool WasCalled { get; private set; }
        public bool ThrowOnGenerate { get; set; }
        public AIProviderResult Result { get; set; } = AIProviderResult.Success("Safe provider response", "Fake", "fake-model", "req-1", 12);

        public Task<AIProviderResult> GenerateAsync(AIProviderRequest request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            if (ThrowOnGenerate)
            {
                throw new InvalidOperationException("provider failure secret");
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class CapturingValidator(IAIResponseValidator? innerValidator = null) : IAIResponseValidator
    {
        public bool WasCalled { get; private set; }
        public AIResponseValidationResult Result { get; set; } = new() { Outcome = AIResponseValidationOutcome.Valid };
        public AIResponseValidationRequest? LastRequest { get; private set; }

        public AIResponseValidationResult Validate(AIResponseValidationRequest request)
        {
            WasCalled = true;
            LastRequest = request;
            return innerValidator?.Validate(request) ?? Result;
        }
    }

    private sealed class FakeAIContextRepository : IAIContextRepository
    {
        public List<AuditLog> AuditLogs { get; } = [];

        public Task<Guest?> GetGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken) => Task.FromResult<Guest?>(null);
        public Task<int> CountCompletedReservationsForGuestAsync(Guid companyId, Guid guestId, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<Reservation?> GetReservationAsync(Guid companyId, Guid reservationId, CancellationToken cancellationToken) => Task.FromResult<Reservation?>(null);
        public Task<Property?> GetPropertyContextAsync(Guid companyId, Guid propertyId, CancellationToken cancellationToken) => Task.FromResult<Property?>(null);
        public Task<Conversation?> GetConversationAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken) => Task.FromResult<Conversation?>(null);

        public Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken)
        {
            AuditLogs.Add(auditLog);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCurrentTenantContext : ICurrentTenantContext
    {
        public Guid? CompanyId { get; } = Guid.NewGuid();
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? CorrelationId { get; } = "orchestration-test";
        public bool IsAuthenticated { get; } = true;
    }
}
