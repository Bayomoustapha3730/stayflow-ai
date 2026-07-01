using Microsoft.AspNetCore.Mvc;

namespace StayFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "StayFlow AI Backend",
            status = "Ready"
        });
    }
}
