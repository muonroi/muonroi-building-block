namespace Quickstart.AspNetCore.Api.Models;

/// <summary>
/// A simple weather forecast DTO returned by the quickstart controller.
/// </summary>
public record WeatherForecast(DateOnly Date, int TemperatureC, string Summary)
{
    /// <summary>Temperature in Fahrenheit, derived from <see cref="TemperatureC"/>.</summary>
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
