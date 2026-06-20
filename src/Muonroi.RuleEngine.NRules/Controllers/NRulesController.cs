using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.RuleEngine.NRules.Controllers;

/// <summary>
/// API controller exposing CRUD and test endpoints for NRules rule definitions
/// under the <c>/api/v1/rule-engine</c> route prefix.
/// </summary>
/// <param name="services">Application service provider used to resolve <see cref="NRulesEngine"/> at test time.</param>
/// <param name="dateTimeService">UTC clock service used to stamp rule save and test execution timestamps.</param>
[ApiController]
[Route("api/v1/rule-engine")]
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed class NRulesController(IServiceProvider services, IMDateTimeService dateTimeService) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, NRulesDefinitionDto> Definitions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns all stored NRules definitions ordered by name.</summary>
    [HttpGet("nrules")]
    public IActionResult List()
    {
        return Ok(Definitions.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Returns the NRules definition identified by <paramref name="id"/>, or 404 if not found.</summary>
    /// <param name="id">The unique identifier of the rule definition.</param>
    [HttpGet("nrules/{id}")]
    public IActionResult Get(string id)
    {
        if (!Definitions.TryGetValue(id, out NRulesDefinitionDto? definition))
        {
            return NotFound();
        }

        return Ok(definition);
    }

    /// <summary>Creates or replaces the NRules definition for <paramref name="id"/> and stamps the current UTC time.</summary>
    /// <param name="id">The unique identifier to assign to the rule definition.</param>
    /// <param name="definition">The rule definition payload from the request body.</param>
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

    /// <summary>
    /// Executes the NRules engine against the facts supplied in <paramref name="request"/>
    /// and returns a summary of the test run.
    /// </summary>
    /// <param name="request">Contains the target rule identifier and the list of facts to evaluate.</param>
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

/// <summary>Data transfer object representing a stored NRules rule definition.</summary>
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed record NRulesDefinitionDto
{
    /// <summary>Gets the unique identifier for this rule definition.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the human-readable name of the rule.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets an optional description explaining the rule's purpose.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the NRules condition expression that determines when the rule matches.</summary>
    public string RuleExpression { get; init; } = string.Empty;

    /// <summary>Gets the NRules action expression executed when the rule condition is satisfied.</summary>
    public string ActionExpression { get; init; } = string.Empty;

    /// <summary>Gets the UTC timestamp of the most recent save operation.</summary>
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
}

/// <summary>Request payload for the NRules test endpoint containing a rule identifier and a set of facts to evaluate.</summary>
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed record NRulesTestRequest
{
    /// <summary>Gets the identifier of the rule definition to test.</summary>
    public string RuleId { get; init; } = string.Empty;

    /// <summary>Gets the list of fact objects, each represented as a property-bag dictionary, to insert into the NRules session.</summary>
    public List<Dictionary<string, object?>> Facts { get; init; } = [];
}
