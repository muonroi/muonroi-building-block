using System.Text;
using Muonroi.RuleEngine.Abstractions.Models;

namespace Muonroi.RuleEngine.DecisionTable.Import;

/// <summary>
/// Provides methods to export raw decision tables to different formats.
/// </summary>
public static class DecisionTableExporter
{
    /// <summary>
    /// Exports the specified raw decision table to a CSV string.
    /// </summary>
    /// <param name="table">The raw decision table to export.</param>
    /// <returns>A CSV string representation of the decision table.</returns>
    public static string ExportCsv(RawDecisionTable table)
    {
        StringBuilder sb = new();
        sb.AppendLine($"HitPolicy,{table.HitPolicy}");
        sb.AppendLine(string.Join(',', table.InputHeaders.Concat(table.OutputHeaders)));
        foreach (RawDecisionRule rule in table.Rules)
        {
            List<string> cols = [];
            foreach (string header in table.InputHeaders)
            {
                cols.Add(rule.Inputs[header]);
            }

            foreach (string header in table.OutputHeaders)
            {
                cols.Add(rule.Outputs[header]);
            }

            sb.AppendLine(string.Join(',', cols));
        }

        return sb.ToString();
    }
}
