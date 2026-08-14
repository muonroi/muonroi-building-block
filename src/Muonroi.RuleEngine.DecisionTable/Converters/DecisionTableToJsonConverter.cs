namespace Muonroi.RuleEngine.DecisionTable.Converters;

/// <summary>
/// Converts a decision table to JSON workflow compatible with RulesEngineService.
/// </summary>
/// <param name="jsonSerializeService">Optional JSON serializer for output payloads.</param>
public sealed class DecisionTableToJsonConverter(IMJsonSerializeService? jsonSerializeService = null) : IDecisionTableConverter
{
    private readonly IMJsonSerializeService _jsonSerializeService =
        jsonSerializeService ?? new MJsonSerializeService();

    /// <summary>
    /// Converts a decision table into a workflow JSON string.
    /// </summary>
    /// <param name="table">Decision table to convert.</param>
    /// <returns>Serialized workflow JSON.</returns>
    public string Convert(DecisionTableModel table)
    {
        DecisionTableRow[] enabledRows = [.. table.Rows
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)];

        var workflow = new[]
        {
            new
            {
                WorkflowName = string.IsNullOrWhiteSpace(table.Name)
                    ? $"{DecisionTableConstants.DefaultWorkflowNamePrefix}_{table.Id}"
                    : table.Name,
                Tenant = table.TenantId ?? "default",
                Rules = enabledRows.Select((row, index) => new
                {
                    RuleName = row.Id,
                    Description = row.Description,
                    RuleExpressionType = "LambdaExpression",
                    Expression = BuildCombinedExpression(table, row),
                    Actions = new
                    {
                        OnSuccess = new
                        {
                            Name = "OutputExpression",
                            Context = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["expression"] = BuildActionOutputExpression(table, row)
                            }
                        }
                    },
                    Properties = new Dictionary<string, object?>
                    {
                        ["Priority"] = enabledRows.Length - index,
                        ["HitPolicy"] = table.HitPolicy.ToString()
                    }
                }).ToArray()
            }
        };

        return JsonSerializer.Serialize(workflow, new JsonSerializerOptions { WriteIndented = true }); // MBB002-exempt: requires WriteIndented formatting for workflow JSON output not available in wrapper
    }

    private static string BuildCombinedExpression(DecisionTableModel table, DecisionTableRow row)
    {
        IEnumerable<string> conditions = row.InputCells
            .Select((cell, index) =>
            {
                if (index >= table.InputColumns.Count)
                {
                    return "true";
                }

                DecisionTableColumn column = table.InputColumns[index];
                string expr = cell.Expression.Trim();
                if (string.IsNullOrWhiteSpace(expr) || expr is "-" or "*" or "any")
                {
                    return "true";
                }

                if (expr.StartsWith(">") || expr.StartsWith("<") || expr.StartsWith("=") || expr.StartsWith("!"))
                {
                    return $"input1.value.{column.Name} {expr}";
                }

                if (expr.StartsWith("between ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = expr[8..].Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        return $"(input1.value.{column.Name} >= {parts[0]} && input1.value.{column.Name} <= {parts[1]})";
                    }
                }

                if (expr.StartsWith("(") && expr.EndsWith(")"))
                {
                    string list = expr[1..^1];
                    IEnumerable<string> values = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x => $"\"{x.Trim('\"', '\'')}\"");
                    return $"new [] {{ {string.Join(", ", values)} }}.Contains(input1.value.{column.Name}.ToString())";
                }

                return $"input1.value.{column.Name}.ToString() == \"{expr.Trim('\"', '\'')}\"";
            });

        return string.Join(" && ", conditions);
    }

    private string BuildActionOutputExpression(DecisionTableModel table, DecisionTableRow row)
    {
        Dictionary<string, object?> outputMap = [];
        for (int i = 0; i < row.OutputCells.Count && i < table.OutputColumns.Count; i++)
        {
            DecisionTableColumn column = table.OutputColumns[i];
            outputMap[column.Name] = ParseLiteral(row.OutputCells[i].Expression);
        }

        return _jsonSerializeService.Serialize(outputMap);
    }

    private static object? ParseLiteral(string expression)
    {
        string value = expression.Trim().Trim('\'', '"');
        if (bool.TryParse(value, out bool boolValue))
        {
            return boolValue;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long longValue))
        {
            return longValue;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
        {
            return doubleValue;
        }

        return value;
    }
}
