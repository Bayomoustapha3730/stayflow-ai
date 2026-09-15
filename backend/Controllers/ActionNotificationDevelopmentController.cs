using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayFlow.Api.Authorization;
using StayFlow.Api.Common;
using StayFlow.Api.Services.ConciergeActions;

namespace StayFlow.Api.Controllers;

/// <summary>
/// TEMPORARY Development-only endpoint that runs exactly one action notification delivery cycle
/// so E2E validation can drain the outbox deterministically without enabling the background worker.
/// Never available outside the Development environment.
/// </summary>
[ApiController]
[Route("development/action-notifications")]
[Produces("application/json")]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ActionNotificationDevelopmentController(
    IWebHostEnvironment environment,
    IActionNotificationDeliveryProcessor processor) : ControllerBase
{
    [HttpPost("process-once")]
    [RequiresPermission("conversations.manage")]
    [ProducesResponseType(typeof(ApiResponse<ActionNotificationDeliveryResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ActionNotificationDeliveryResult>>> ProcessOnce(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await processor.ProcessDueAsync(cancellationToken);
        return Ok(ApiResponse<ActionNotificationDeliveryResult>.Ok(result));
    }
}
