using Microsoft.AspNetCore.Mvc;
using Quickstart.AspNetCore.Api.Models;

namespace Quickstart.AspNetCore.Api.Controllers;

/// <summary>
/// A minimal controller exercising the standard ASP.NET Core MVC surface
/// that Muonroi.AspNetCore's AddBaseApi() configures (versioning + Swagger).
/// </summary>
[ApiController]
[Route("api/weather")]
public sealed class WeatherController : ControllerBase
{
    private static readonly string[] Summaries =
        ["Freezing", "Cool", "Mild", "Warm", "Scorching"];

    // GET api/weather?days=5
    // A plain MVC action — appears in the versioned Swagger doc produced by AddBaseApi().
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WeatherForecast>), StatusCodes.Status200OK)]
    public IActionResult Get([FromQuery] int days = 5)
    {
        IEnumerable<WeatherForecast> forecast = Enumerable.Range(1, Math.Clamp(days, 1, 14))
            .Select(offset => new WeatherForecast(
                Date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(offset)),
                TemperatureC: Random.Shared.Next(-20, 40),
                Summary: Summaries[Random.Shared.Next(Summaries.Length)]));

        return Ok(forecast);
    }
}
