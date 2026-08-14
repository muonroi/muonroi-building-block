namespace Muonroi.RuleEngine.DecisionTable.Import;

/// <summary>
/// Provides methods to import decision tables from different formats into <see cref="RawDecisionTable"/>.
/// </summary>
public static partial class DecisionTableImporter
{
    [GeneratedRegex(@"^\[(?<min>-?\d+(?:\.\d+)?)\.\.(?<max>-?\d+(?:\.\d+)?)\]$", RegexOptions.CultureInvariant)]
    private static partial Regex RangeRegex();

    [GeneratedRegex(@"^(?<op>>=|>)\s*(?<val>-?\d+(?:\.\d+)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex GreaterRegex();

    [GeneratedRegex(@"^(?<op><=|<)\s*(?<val>-?\d+(?:\.\d+)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex LessRegex();

    [GeneratedRegex(@"^(?<val>-?\d+(?:\.\d+)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex EqualRegex();

    private readonly record struct Interval(double Min, double Max, bool IncludeMin, bool IncludeMax);

    /// <summary>
    /// Imports a decision table from a CSV string.
    /// </summary>
    /// <param name="csvContent">The CSV content to import.</param>
    /// <returns>A <see cref="RawDecisionTable"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the CSV content is empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the CSV content is malformed.</exception>
    public static RawDecisionTable ImportCsv(string csvContent)
    {
        MGuard.NotEmpty(csvContent, nameof(csvContent));

        string[] lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        MGuard.State(lines.Length >= 3, "Table must contain hit policy, headers and at least one rule");

        string[] hitParts = lines[0].Split(',');
        MGuard.State(hitParts.Length >= 2 && hitParts[0].Equals("HitPolicy", StringComparison.OrdinalIgnoreCase), "Missing HitPolicy declaration");

        MGuard.State(Enum.TryParse(hitParts[1], true, out RawHitPolicy policy), $"Invalid hit policy {hitParts[1]}");

        string[] headers = lines[1].Split(',');
        MGuard.State(headers.Length >= 2, "Decision table requires at least one input and one output column");

        RawDecisionTable table = new()
        {
            HitPolicy = policy
        };
        table.InputHeaders.Add(headers[0].Trim());
        for (int i = 1; i < headers.Length; i++)
        {
            table.OutputHeaders.Add(headers[i].Trim());
        }

        for (int rowIndex = 2; rowIndex < lines.Length; rowIndex++)
        {
            string[] cols = lines[rowIndex].Split(',');
            MGuard.State(cols.Length == headers.Length, $"Row {rowIndex - 1} has incorrect number of columns");

            Dictionary<string, string> inputs = new()
            {
                [table.InputHeaders[0]] = cols[0].Trim()
            };
            Dictionary<string, string> outputs = [];
            for (int j = 1; j < cols.Length; j++)
            {
                outputs[table.OutputHeaders[j - 1]] = cols[j].Trim();
            }

            table.Rules.Add(new RawDecisionRule(inputs, outputs));
        }

        DetectOverlap(table);
        return table;
    }

    private static void DetectOverlap(RawDecisionTable table)
    {
        if (table.InputHeaders.Count != 1)
        {
            return;
        }

        string header = table.InputHeaders[0];

        for (int i = 0; i < table.Rules.Count; i++)
        {
            if (!TryParseRange(table.Rules[i].Inputs[header], out Interval a))
            {
                continue;
            }

            for (int j = i + 1; j < table.Rules.Count; j++)
            {
                if (!TryParseRange(table.Rules[j].Inputs[header], out Interval b))
                {
                    continue;
                }

                if (IntervalsOverlap(a, b))
                {
                    table.Warnings.Add($"Overlap detected between rows {i + 1} and {j + 1}");
                }
            }
        }
    }

    private static bool IntervalsOverlap(Interval a, Interval b)
    {
        if (a.Max < b.Min)
        {
            return false;
        }

        if (a.Max == b.Min && !(a.IncludeMax && b.IncludeMin))
        {
            return false;
        }

        if (b.Max < a.Min)
        {
            return false;
        }

        if (b.Max == a.Min && !(b.IncludeMax && a.IncludeMin))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseRange(string expr, out Interval interval)
    {
        expr = expr.Trim();

        Match m = RangeRegex().Match(expr);
        if (m.Success)
        {
            if (TryD(m.Groups["min"].Value, out double min) && TryD(m.Groups["max"].Value, out double max))
            {
                interval = new Interval(min, max, true, true);
                return true;
            }
        }

        m = GreaterRegex().Match(expr);
        if (m.Success && TryD(m.Groups["val"].Value, out double v1))
        {
            bool includeMin = m.Groups["op"].Value == ">=";
            interval = new Interval(v1, double.PositiveInfinity, includeMin, false);
            return true;
        }

        m = LessRegex().Match(expr);
        if (m.Success && TryD(m.Groups["val"].Value, out double v2))
        {
            bool includeMax = m.Groups["op"].Value == "<=";
            interval = new Interval(double.NegativeInfinity, v2, false, includeMax);
            return true;
        }

        m = EqualRegex().Match(expr);
        if (m.Success && TryD(m.Groups["val"].Value, out double v3))
        {
            interval = new Interval(v3, v3, true, true);
            return true;
        }

        interval = default;
        return false;
    }

    private static bool TryD(string s, out double d)
    {
        return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture,
            out d);
    }
}
