using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.Onboarding;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/onboarding")]
[Produces("application/json")]
public sealed class OnboardingController(IOnboardingService onboardingService) : ControllerBase
{
    [HttpGet("status")]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> GetStatus(CancellationToken cancellationToken)
    {
        var response = await onboardingService.GetStatusAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("start")]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> Start(CancellationToken cancellationToken)
    {
        var response = await onboardingService.StartAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("organization/complete")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteOrganization(
        [FromBody] CompleteOnboardingOrganizationStepRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteOrganizationStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("plan/complete")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompletePlan(
        [FromBody] CompleteOnboardingPlanStepRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompletePlanStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("property/complete")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteProperty(
        [FromBody] CompleteOnboardingPropertyStepRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompletePropertyStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("team/complete")]
    [Authorize(Policy = OrganizationPolicyNames.Manager)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteTeam(
        [FromBody] CompleteOnboardingTeamStepRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteTeamStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("complete")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> Complete(CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteOnboardingAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}