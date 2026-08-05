using Microsoft.AspNetCore.Mvc;

namespace StayFlow.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    [HttpGet]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "StayFlow AI Backend",
            status = "Ready"
        });
    }
}
