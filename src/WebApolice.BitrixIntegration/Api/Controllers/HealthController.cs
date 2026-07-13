using Microsoft.AspNetCore.Mvc;
using System;

namespace WebApolice.BitrixIntegration.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            application = "WebApolice.BitrixIntegration",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            timestamp = DateTime.UtcNow
        });
    }
}
