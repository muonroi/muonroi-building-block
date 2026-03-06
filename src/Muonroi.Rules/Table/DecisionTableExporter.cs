using System.Text;

namespace Muonroi.Rules.Table;

public static class DecisionTableExporter
{
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