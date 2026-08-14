namespace Quickstart.RuleEngine.Runtime.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RuntimeController : ControllerBase
{
    private readonly RulesEngineService _rulesEngine;

    public RuntimeController(RulesEngineService rulesEngine)
    {
        _rulesEngine = rulesEngine;
    }

    [HttpPost("execute/{ruleSetId}")]
    public async Task<IActionResult> Execute(string ruleSetId, [FromBody] Dictionary<string, object> context, CancellationToken ct)
    {
        // Execute rule set against a context (using Dictionary as dynamic context)
        var result = await _rulesEngine.ExecuteAsync(ruleSetId, "1.0", context, ct);
        
        return Ok(new
        {
            Success = result.Success,
            Errors = result.Errors,
            Facts = result.Facts.AsReadOnly()
        });
    }

    [HttpPost("deploy")]
    public async Task<IActionResult> Deploy(CancellationToken ct)
    {
        var ruleSet = new RuleSetRecord
        {
            Id = "hello-world",
            Name = "Hello World Rules",
            Version = "1.0",
            Status = RuleSetStatus.Active,
            Definitions = new List<RuleDefinition>
            {
                new RuleDefinition
                {
                    Code = "SAY_HELLO",
                    Type = "Business",
                    AdapterType = "Feel",
                    Expression = "if context.Name != null then \"Hello, \" + context.Name else \"Hello, World!\"",
                    OutputField = "Greeting"
                }
            }
        };

        await _rulesEngine.CreateRuleSetAsync(ruleSet, ct);
        return Ok(new { Message = "Deployed hello-world rule set v1.0" });
    }
}
