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

    [HttpPost("organization")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteOrganization(
        [FromBody] OnboardingOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteOrganizationStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("plan")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompletePlan(
        [FromBody] OnboardingPlanRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompletePlanStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("property")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteProperty(
        [FromBody] OnboardingPropertyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompletePropertyStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("invitations")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingActionResponse<OnboardingInvitationsResponse>>>> CompleteInvitations(
        [FromBody] OnboardingInvitationsRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteInvitationsStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("whatsapp")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteWhatsApp(
        [FromBody] OnboardingWhatsAppRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteWhatsAppStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("ai-provider")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteAiProvider(
        [FromBody] OnboardingAiProviderRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteAiProviderStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("knowledge")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteKnowledge(
        [FromBody] OnboardingKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteKnowledgeStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("demo-data")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> CompleteDemoData(
        [FromBody] OnboardingDemoDataRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteDemoDataStepAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("steps/{step}/skip")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> SkipStep(
        [FromRoute] string step,
        [FromBody] OnboardingSkipStepRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.SkipStepAsync(step, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("complete")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> Complete(
        [FromBody] OnboardingCompleteRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.CompleteOnboardingAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("reset")]
    [Authorize(Policy = OrganizationPolicyNames.PlatformAdmin)]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> Reset(
        [FromBody] OnboardingResetRequest request,
        CancellationToken cancellationToken)
    {
        var response = await onboardingService.ResetAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}