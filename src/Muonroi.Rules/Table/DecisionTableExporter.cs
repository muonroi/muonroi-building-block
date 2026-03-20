using System.Text;

namespace Muonroi.Rules.Table;

/// <summary>
/// Provides methods to export decision tables to different formats.
/// </summary>
public static class DecisionTableExporter
{
    /// <summary>
    /// Exports the specified decision table to a CSV string.
    /// </summary>
    /// <param name="table">The decision table to export.</param>
    /// <returns>A CSV string representation of the decision table.</returns>
    public static string ExportCsv(DecisionTable table)
    {
        StringBuilder sb = new();
        sb.AppendLine($"HitPolicy,{table.HitPolicy}");
        sb.AppendLine(string.Join(',', table.InputHeaders.Concat(table.OutputHeaders)));
        foreach (DecisionRule rule in table.Rules)
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