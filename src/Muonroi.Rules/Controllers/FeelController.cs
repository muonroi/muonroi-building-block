using Microsoft.AspNetCore.Mvc;

namespace Muonroi.Rules.Controllers;

[ApiController]
[Route("api/v1/feel")]
public sealed class FeelController : FeelControllerBase
{
}

public sealed record FeelEvaluateRequest
{
    public string Expression { get; init; } = string.Empty;
    public Dictionary<string, JsonElement> Context { get; init; } = [];
}

public sealed record FeelAutocompleteRequest
{
    public string PartialExpression { get; init; } = string.Empty;
    public string? DataType { get; init; }
    public Dictionary<string, JsonElement>? Context { get; init; }
}
