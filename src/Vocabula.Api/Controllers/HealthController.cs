using Microsoft.AspNetCore.Mvc;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy" });
    }
}
