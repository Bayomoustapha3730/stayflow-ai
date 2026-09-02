using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.DTOs.WhatsApp;
using StayFlow.Api.Models;
using StayFlow.Api.Services;

namespace StayFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("whatsapp/integrations")]
[Produces("application/json")]
[RequireFeature(FeatureKeys.WhatsApp)]
public sealed class WhatsAppIntegrationsController(IWhatsAppTemplateService whatsAppTemplateService) : ControllerBase
{
    [HttpGet]
    [RequiresPermission("conversations.read")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<WhatsAppIntegrationSummaryResponse>>>> ListIntegrations(CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.GetIntegrationsAsync(cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("{integrationId:guid}")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<WhatsAppIntegrationDetailResponse>>> GetIntegration(Guid integrationId, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.GetIntegrationDetailAsync(integrationId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPost]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<WhatsAppIntegrationDetailResponse>>> CreateIntegration(WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.CreateIntegrationAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPut("{integrationId:guid}")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<WhatsAppIntegrationDetailResponse>>> UpdateIntegration(Guid integrationId, WhatsAppIntegrationConfigurationRequest request, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.UpdateIntegrationAsync(integrationId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("{integrationId:guid}/production/enable")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<WhatsAppProductionEnableResponse>>> EnableProduction(Guid integrationId, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.EnableProductionAsync(integrationId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("{integrationId:guid}/production/disable")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<WhatsAppProductionEnableResponse>>> DisableProduction(Guid integrationId, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.DisableProductionAsync(integrationId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("{integrationId:guid}/health")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<WhatsAppIntegrationHealthResponse>>> CheckHealth(Guid integrationId, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.CheckHealthAsync(integrationId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPost("{integrationId:guid}/templates/sync")]
    [RequiresPermission("conversations.manage")]
    public async Task<ActionResult<ApiResponse<WhatsAppTemplateSyncResponse>>> SyncTemplates(Guid integrationId, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.SyncTemplatesAsync(integrationId, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("{integrationId:guid}/templates")]
    [RequiresPermission("conversations.read")]
    public async Task<ActionResult<ApiResponse<WhatsAppTemplateListResponse>>> ListTemplates(Guid integrationId, [FromQuery] WhatsAppTemplateListQuery query, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.ListTemplatesAsync(integrationId, query, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpGet("{integrationId:guid}/templates/{templateId:guid}")]
    [RequiresPermission("conversations.read")]
    public async Task<ActionResult<ApiResponse<WhatsAppTemplateDetailResponse>>> GetTemplate(Guid integrationId, Guid templateId, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.GetTemplateAsync(integrationId, templateId, cancellationToken);
        return response.Success ? Ok(response) : NotFound(response);
    }

    [HttpPost("{integrationId:guid}/templates/{templateId:guid}/preview")]
    [RequiresPermission("conversations.read")]
    public async Task<ActionResult<ApiResponse<WhatsAppTemplatePreviewResponse>>> PreviewTemplate(Guid integrationId, Guid templateId, WhatsAppTemplatePreviewRequest request, CancellationToken cancellationToken)
    {
        var response = await whatsAppTemplateService.PreviewTemplateAsync(integrationId, templateId, request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
