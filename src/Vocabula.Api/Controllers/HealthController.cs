using Microsoft.AspNetCore.Mvc;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    // Accept GET and HEAD so that HTTP-level probes (load balancers, status
    // badges such as shields.io, which issue HEAD requests) see the endpoint
    // as healthy instead of a 405.
    [HttpGet]
    [HttpHead]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy" });
    }
}
