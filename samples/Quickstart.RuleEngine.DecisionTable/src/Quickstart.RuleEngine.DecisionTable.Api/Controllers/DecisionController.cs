namespace Quickstart.RuleEngine.DecisionTable.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DecisionController : ControllerBase
{
    private readonly IDecisionTableStore _store;
    private readonly IDecisionTableExecutor _executor;

    public DecisionController(IDecisionTableStore store, IDecisionTableExecutor executor)
    {
        _store = store;
        _executor = executor;
    }

    [HttpPost("setup")]
    public async Task<IActionResult> SetupTable(CancellationToken ct)
    {
        var table = new DecisionTableModel
        {
            Id = "loan-approval",
            Name = "Loan Approval Rules",
            HitPolicy = HitPolicy.First,
            InputColumns = new List<DecisionTableColumn>
            {
                new() { Id = "col1", Name = "CreditScore", DataType = "number" },
                new() { Id = "col2", Name = "Income", DataType = "number" }
            },
            OutputColumns = new List<DecisionTableColumn>
            {
                new() { Id = "out1", Name = "Approved", DataType = "boolean" },
                new() { Id = "out2", Name = "InterestRate", DataType = "number" }
            },
            Rows = new List<DecisionTableRow>
            {
                new()
                {
                    Order = 1,
                    InputCells = new List<DecisionTableCell>
                    {
                        new() { ColumnId = "col1", Expression = ">= 750" },
                        new() { ColumnId = "col2", Expression = ">= 50000" }
                    },
                    OutputCells = new List<DecisionTableCell>
                    {
                        new() { ColumnId = "out1", Expression = "true" },
                        new() { ColumnId = "out2", Expression = "3.5" }
                    }
                },
                new()
                {
                    Order = 2,
                    InputCells = new List<DecisionTableCell>
                    {
                        new() { ColumnId = "col1", Expression = "< 750" },
                        new() { ColumnId = "col2", Expression = "-" } // Any income
                    },
                    OutputCells = new List<DecisionTableCell>
                    {
                        new() { ColumnId = "out1", Expression = "false" },
                        new() { ColumnId = "out2", Expression = "0" }
                    }
                }
            }
        };

        await _store.CreateAsync(table, ct);
        return Ok(new { Message = "Table created", TableId = table.Id });
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] Dictionary<string, object> inputs, CancellationToken ct)
    {
        var table = await _store.GetByIdAsync("loan-approval", ct);
        if (table == null)
            return NotFound("Call /setup first");

        var result = await _executor.ExecuteAsync(table, inputs, ct);
        
        return Ok(new
        {
            HasMatches = result.MatchedRows.Any(),
            Outputs = result.Outputs
        });
    }
}
