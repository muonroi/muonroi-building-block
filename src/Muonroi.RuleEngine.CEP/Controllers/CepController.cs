using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.CEP.Controllers;

[ApiController]
[Route("api/v1/rule-engine/cep")]
public sealed class CepController(IMDateTimeService dateTimeService) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, CepConfigDto> Configs = new(StringComparer.OrdinalIgnoreCase);

    [HttpGet]
    public IActionResult List()
    {
        return Ok(Configs.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
    }

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        if (!Configs.TryGetValue(id, out CepConfigDto? config))
        {
            return NotFound();
        }

        return Ok(config);
    }

    [HttpPut("{id}")]
    public IActionResult Save(string id, [FromBody] CepConfigDto config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            return BadRequest(new { message = "Config name is required." });
        }

        if (!Enum.TryParse(config.WindowType, true, out WindowType _))
        {
            return BadRequest(new { message = $"Unsupported windowType '{config.WindowType}'." });
        }

        CepConfigDto payload = config with
        {
            Id = id,
            UpdatedAtUtc = dateTimeService.UtcNow()
        };

        Configs[id] = payload;
        return Ok(payload);
    }

    [HttpPost("{id}/simulate")]
    public IActionResult Simulate(string id, [FromBody] CepSimulationRequest request)
    {
        if (!Configs.TryGetValue(id, out CepConfigDto? config))
        {
            return NotFound();
        }

        if (!Enum.TryParse(config.WindowType, true, out WindowType windowType))
        {
            return BadRequest(new { message = $"Unsupported windowType '{config.WindowType}'." });
        }

        TimeSpan windowSize = TimeSpan.FromSeconds(Math.Max(1, config.WindowSizeSeconds));
        CepEngine<Dictionary<string, object?>> engine = new(windowSize, windowType, TimeSpan.FromSeconds(Math.Max(1, config.TimeToLiveSeconds)));

        List<object> windows = [];
        foreach (CepSimulationEvent evt in request.Events.OrderBy(x => x.TimestampUtc))
        {
            IReadOnlyList<CepEvent<Dictionary<string, object?>>> window = engine.AddEvent(
                evt.Key ?? "default",
                evt.Payload,
                evt.TimestampUtc.ToUniversalTime());

            windows.Add(new
            {
                evt.Key,
                evt.TimestampUtc,
                count = window.Count
            });
        }

        return Ok(new
        {
            config.Id,
            config.Name,
            processedEvents = request.Events.Count,
            windows
        });
    }
}

public sealed record CepConfigDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string WindowType { get; init; } = nameof(Muonroi.RuleEngine.CEP.WindowType.Sliding);
    public int WindowSizeSeconds { get; init; } = 60;
    public int TimeToLiveSeconds { get; init; } = 300;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}

public sealed record CepSimulationRequest
{
    public List<CepSimulationEvent> Events { get; init; } = [];
}

public sealed record CepSimulationEvent
{
    public string? Key { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    public Dictionary<string, object?> Payload { get; init; } = [];
}
