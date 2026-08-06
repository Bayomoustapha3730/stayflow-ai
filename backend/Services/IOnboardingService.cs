using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Onboarding;

namespace StayFlow.Api.Services;

public interface IOnboardingService
{
    Task<ApiResponse<OnboardingStatusDto>> GetStatusAsync(CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> StartAsync(CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteOrganizationStepAsync(CompleteOnboardingOrganizationStepRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompletePlanStepAsync(CompleteOnboardingPlanStepRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompletePropertyStepAsync(CompleteOnboardingPropertyStepRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteTeamStepAsync(CompleteOnboardingTeamStepRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<OnboardingStatusDto>> CompleteOnboardingAsync(CancellationToken cancellationToken);
}