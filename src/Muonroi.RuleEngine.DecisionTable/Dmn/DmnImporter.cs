using System.Xml;
using System.Xml.Linq;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Dmn;

/// <summary>
/// Imports a DMN 1.3 XML document into a Muonroi <see cref="DecisionTableModel"/>.
/// Supports both DMN 1.3 (20191111) and DMN 1.1/1.2 (20180521) namespaces on import.
/// </summary>
public static class DmnImporter
{
    /// <summary>
    /// Parses a DMN XML string and returns the first decision table found.
    /// </summary>
    /// <param name="xml">The DMN 1.3 XML string to parse.</param>
    /// <returns>
    /// A <see cref="DmnImportResult"/> with the imported table on success,
    /// or error messages on failure. Never throws — all errors are captured in the result.
    /// </returns>
    public static DmnImportResult ImportFromDmnXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return DmnImportResult.Failure("XML input is null or empty.");
        }

        try
        {
            XDocument doc = XDocument.Parse(xml);
            return ParseDocument(doc);
        }
        catch (XmlException ex)
        {
            return DmnImportResult.Failure($"Failed to parse XML: {ex.Message}");
        }
        catch (Exception ex)
        {
            return DmnImportResult.Failure($"Unexpected error during import: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a DMN XML stream and returns the first decision table found.
    /// </summary>
    /// <param name="stream">The stream containing DMN 1.3 XML content.</param>
    /// <returns>A <see cref="DmnImportResult"/> with the imported table or error messages.</returns>
    public static DmnImportResult ImportFromDmnXml(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            XDocument doc = XDocument.Load(stream);
            return ParseDocument(doc);
        }
        catch (XmlException ex)
        {
            return DmnImportResult.Failure($"Failed to parse XML stream: {ex.Message}");
        }
        catch (Exception ex)
        {
            return DmnImportResult.Failure($"Unexpected error during import: {ex.Message}");
        }
    }

    // ── Private parsing logic ────────────────────────────────────────────────

    private static DmnImportResult ParseDocument(XDocument doc)
    {
        XElement? definitions = doc.Root;
        if (definitions is null)
        {
            return DmnImportResult.Failure("XML document has no root element.");
        }

        // Support both DMN 1.3 and legacy DMN 1.1/1.2 namespaces
        XNamespace ns = definitions.Name.NamespaceName;
        if (ns != DmnConstants.DmnNamespace && ns != DmnConstants.DmnLegacyNamespace)
        {
            // Accept any namespace — be permissive on import to support non-standard producers
            // If namespace is unknown, we still attempt to parse by local name
        }

        // Collect all <decision> elements
        List<XElement> decisions = [.. definitions.Elements(ns + DmnConstants.Decision)];
        if (decisions.Count == 0)
        {
            return DmnImportResult.Failure("No <decision> element found in DMN definitions.");
        }

        List<string> warnings = [];
        if (decisions.Count > 1)
        {
            warnings.Add($"Multiple decisions found ({decisions.Count}). Importing first decision only.");
        }

        XElement decision = decisions[0];

        // Find <decisionTable> inside <decision>
        XElement? decisionTable = decision.Element(ns + DmnConstants.DecisionTable);
        if (decisionTable is null)
        {
            return DmnImportResult.Failure(
                $"Decision '{decision.Attribute(DmnConstants.AttrId)?.Value}' has no <decisionTable> child element.");
        }

        // Parse hit policy
        string hitPolicyStr  = decisionTable.Attribute(DmnConstants.AttrHitPolicy)?.Value ?? "UNIQUE";
        string? aggregation  = decisionTable.Attribute(DmnConstants.AttrAggregation)?.Value;
        HitPolicy hitPolicy  = DmnHitPolicyMapper.FromDmnString(hitPolicyStr, aggregation);

        // Parse input columns
        List<DecisionTableColumn> inputColumns = [];
        foreach (XElement input in decisionTable.Elements(ns + DmnConstants.Input))
        {
            inputColumns.Add(ParseInputColumn(input, ns));
        }

        // Parse output columns
        List<DecisionTableColumn> outputColumns = [];
        foreach (XElement output in decisionTable.Elements(ns + DmnConstants.Output))
        {
            outputColumns.Add(ParseOutputColumn(output));
        }

        // Parse rules (rows)
        List<DecisionTableRow> rows = [];
        int order = 1;
        foreach (XElement rule in decisionTable.Elements(ns + DmnConstants.Rule))
        {
            rows.Add(ParseRule(rule, ns, inputColumns, outputColumns, order));
            order++;
        }

        DecisionTableModel table = new()
        {
            Id          = decision.Attribute(DmnConstants.AttrId)?.Value    ?? Guid.NewGuid().ToString(),
            Name        = decision.Attribute(DmnConstants.AttrName)?.Value  ?? string.Empty,
            HitPolicy   = hitPolicy,
            InputColumns  = inputColumns,
            OutputColumns = outputColumns,
            Rows          = rows
        };

        return DmnImportResult.Success(table, warnings.Count > 0 ? warnings : null);
    }

    private static DecisionTableColumn ParseInputColumn(XElement input, XNamespace ns)
    {
        XElement? inputExpr = input.Element(ns + DmnConstants.InputExpression);
        string name    = inputExpr?.Element(ns + DmnConstants.Text)?.Value ?? string.Empty;
        string label   = input.Attribute(DmnConstants.AttrLabel)?.Value    ?? name;
        string typeRef = inputExpr?.Attribute(DmnConstants.AttrTypeRef)?.Value ?? "string";
        string id      = input.Attribute(DmnConstants.AttrId)?.Value ?? Guid.NewGuid().ToString();

        return new DecisionTableColumn
        {
            Id       = id,
            Name     = name,
            Label    = label,
            DataType = typeRef
        };
    }

    private static DecisionTableColumn ParseOutputColumn(XElement output)
    {
        string name    = output.Attribute(DmnConstants.AttrName)?.Value    ?? string.Empty;
        string label   = output.Attribute(DmnConstants.AttrLabel)?.Value   ?? name;
        string typeRef = output.Attribute(DmnConstants.AttrTypeRef)?.Value ?? "string";
        string id      = output.Attribute(DmnConstants.AttrId)?.Value      ?? Guid.NewGuid().ToString();

        return new DecisionTableColumn
        {
            Id       = id,
            Name     = name,
            Label    = label,
            DataType = typeRef
        };
    }

    private static DecisionTableRow ParseRule(
        XElement rule,
        XNamespace ns,
        IReadOnlyList<DecisionTableColumn> inputColumns,
        IReadOnlyList<DecisionTableColumn> outputColumns,
        int order)
    {
        string id          = rule.Attribute(DmnConstants.AttrId)?.Value ?? Guid.NewGuid().ToString();
        string? description = rule.Element(ns + DmnConstants.Description)?.Value;

        // Match inputEntry elements by position to input columns
        List<XElement> inputEntries  = [.. rule.Elements(ns + DmnConstants.InputEntry)];
        List<XElement> outputEntries = [.. rule.Elements(ns + DmnConstants.OutputEntry)];

        List<DecisionTableCell> inputCells = [];
        for (int i = 0; i < inputEntries.Count; i++)
        {
            string expression = inputEntries[i].Element(ns + DmnConstants.Text)?.Value ?? string.Empty;
            string columnId   = i < inputColumns.Count ? inputColumns[i].Id : string.Empty;
            inputCells.Add(new DecisionTableCell { ColumnId = columnId, Expression = expression });
        }

        List<DecisionTableCell> outputCells = [];
        for (int i = 0; i < outputEntries.Count; i++)
        {
            string expression = outputEntries[i].Element(ns + DmnConstants.Text)?.Value ?? string.Empty;
            string columnId   = i < outputColumns.Count ? outputColumns[i].Id : string.Empty;
            outputCells.Add(new DecisionTableCell { ColumnId = columnId, Expression = expression });
        }

        return new DecisionTableRow
        {
            Id          = id,
            Order       = order,
            Description = string.IsNullOrEmpty(description) ? null : description,
            InputCells  = inputCells,
            OutputCells = outputCells,
            IsEnabled   = true  // DMN has no disable concept — all imported rows are enabled
        };
    }
}
