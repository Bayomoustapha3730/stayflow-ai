using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.ApiKeys;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organization/api-keys")]
[Produces("application/json")]
public sealed class OrganizationApiKeysController(ITenantApiKeyService apiKeyService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = OrganizationPolicyNames.ReadOnly)]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantApiKeyDto>>>> List(CancellationToken cancellationToken)
    {
        var response = await apiKeyService.ListAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<CreateTenantApiKeyResponse>>> Create(
        [FromBody] CreateTenantApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var response = await apiKeyService.CreateAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = OrganizationPolicyNames.Administrator)]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var response = await apiKeyService.RevokeAsync(id, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}

[ApiController]
[Route("api/integrations")]
[Produces("application/json")]
public sealed class ApiKeyIntegrationsController : ControllerBase
{
    [HttpGet("status")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme, Policy = ApiKeyPolicyNames.IntegrationsRead)]
    public ActionResult<ApiResponse<object>> GetStatus()
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            Status = "ok",
            Auth = "api_key",
            Timestamp = DateTimeOffset.UtcNow
        }));
    }
}