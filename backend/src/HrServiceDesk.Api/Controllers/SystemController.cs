using System.Reflection;
using HrServiceDesk.Api.ErrorHandling;
using Microsoft.AspNetCore.Mvc;

namespace HrServiceDesk.Api.Controllers;

/// <summary>Service metadata, used by the SPA to show which API it is talking to.</summary>
[Route("api/system")]
public sealed class SystemController(IHostEnvironment environment) : ApiControllerBase
{
    public sealed record SystemInfoDto(string Name, string Version, string Environment);

    /// <summary>Returns the service name, build version and environment.</summary>
    [HttpGet("info")]
    [ProducesResponseType<SystemInfoDto>(StatusCodes.Status200OK)]
    public ActionResult<SystemInfoDto> GetInfo()
    {
        var version = typeof(SystemController).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
        return Ok(new SystemInfoDto("HR Service Desk API", version, environment.EnvironmentName));
    }
}
