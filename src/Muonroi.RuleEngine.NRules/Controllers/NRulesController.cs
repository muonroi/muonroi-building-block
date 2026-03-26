using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.NRules.Controllers;

[ApiController]
[Route("api/v1/rule-engine")]
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed class NRulesController(IServiceProvider services, IMDateTimeService dateTimeService) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, NRulesDefinitionDto> Definitions = new(StringComparer.OrdinalIgnoreCase);

    [HttpGet("nrules")]
    public IActionResult List()
    {
        return Ok(Definitions.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
    }

    [HttpGet("nrules/{id}")]
    public IActionResult Get(string id)
    {
        if (!Definitions.TryGetValue(id, out NRulesDefinitionDto? definition))
        {
            return NotFound();
        }

        return Ok(definition);
    }

    [HttpPut("nrules/{id}")]
    public IActionResult Save(string id, [FromBody] NRulesDefinitionDto definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return BadRequest(new { message = "Rule definition name is required." });
        }

        NRulesDefinitionDto payload = definition with
        {
            Id = id,
            UpdatedAtUtc = dateTimeService.UtcNow()
        };

        Definitions[id] = payload;
        return Ok(payload);
    }

    [HttpPost("test")]
    public IActionResult Test([FromBody] NRulesTestRequest request)
    {
        if (!Definitions.TryGetValue(request.RuleId, out _))
        {
            return NotFound(new { message = "Rule definition not found." });
        }

        NRulesEngine? engine = services.GetService(typeof(NRulesEngine)) as NRulesEngine;
        try
        {
            if (engine is not null)
            {
                object[] facts = request.Facts.Select(x => (object)x).ToArray();
                engine.Fire(facts);
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "NRules execution failed.", detail = ex.Message });
        }

        return Ok(new
        {
            request.RuleId,
            factCount = request.Facts.Count,
            executedAtUtc = dateTimeService.UtcNow(),
            engineAvailable = engine is not null
        });
    }
}

[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed record NRulesDefinitionDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string RuleExpression { get; init; } = string.Empty;
    public string ActionExpression { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}

[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed record NRulesTestRequest
{
    public string RuleId { get; init; } = string.Empty;
    public List<Dictionary<string, object?>> Facts { get; init; } = [];
}
