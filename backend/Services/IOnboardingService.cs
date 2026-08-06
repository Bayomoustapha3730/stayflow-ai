using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Onboarding;

namespace StayFlow.Api.Services;

public interface IOnboardingService
{
    Task<ApiResponse<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> StartAsync(CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteOrganizationStepAsync(OnboardingOrganizationRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompletePlanStepAsync(OnboardingPlanRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompletePropertyStepAsync(OnboardingPropertyRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>> CompleteInvitationsStepAsync(OnboardingInvitationsRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteWhatsAppStepAsync(OnboardingWhatsAppRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteAiProviderStepAsync(OnboardingAiProviderRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteKnowledgeStepAsync(OnboardingKnowledgeRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteDemoDataStepAsync(OnboardingDemoDataRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> SkipStepAsync(string step, OnboardingSkipStepRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteOnboardingAsync(OnboardingCompleteRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> ResetAsync(OnboardingResetRequest request, CancellationToken cancellationToken);
}