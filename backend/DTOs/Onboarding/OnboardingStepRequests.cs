namespace StayFlow.Api.DTOs.Onboarding;

public sealed class OnboardingOrganizationRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? SupportContactEmail { get; init; }
    public string? TimeZone { get; init; }
    public string? Locale { get; init; }
    public string? BrandingLogoUrl { get; init; }
    public string? BrandingPrimaryColor { get; init; }
}

public sealed class OnboardingPlanRequest
{
    public string? PlanName { get; init; }
}

public sealed class OnboardingPropertyRequest
{
    public string Name { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string CountryCode { get; init; } = "KE";
    public string TimeZone { get; init; } = "Africa/Nairobi";
    public string? Description { get; init; }
    public string? IdempotencyKey { get; init; }
}

public sealed class OnboardingInvitationRequest
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = "Host";
}

public sealed class OnboardingInvitationsRequest
{
    public IReadOnlyCollection<OnboardingInvitationRequest> Invitations { get; init; } = [];
}

public sealed class OnboardingInvitationsResponse
{
    public IReadOnlyCollection<OnboardingInvitationResultDto> Results { get; init; } = [];
}

public sealed class OnboardingInvitationResultDto
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class OnboardingWhatsAppRequest
{
    public Guid? IntegrationId { get; init; }
    public bool RunHealthCheck { get; init; } = true;
}

public sealed class OnboardingAiProviderRequest
{
    public bool AcknowledgeDeterministicFallback { get; init; }
    public bool SkipIfDeterministicOnly { get; init; }
}

public sealed class OnboardingKnowledgeRequest
{
    public Guid? PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public string? IdempotencyKey { get; init; }
}

public sealed class OnboardingDemoDataRequest
{
    public bool CreateSampleKnowledge { get; init; } = true;
    public bool CreateSampleReservation { get; init; } = true;
    public bool CreateSampleConversation { get; init; } = true;
    public bool CreateSampleHostCopilotItem { get; init; } = true;
    public string? IdempotencyKey { get; init; }
}

public sealed class OnboardingSkipStepRequest
{
    public string? Reason { get; init; }
}

public sealed class OnboardingCompleteRequest
{
    public bool ConfirmChecklistReviewed { get; init; }
}

public sealed class OnboardingResetRequest
{
    public bool Confirm { get; init; }
}