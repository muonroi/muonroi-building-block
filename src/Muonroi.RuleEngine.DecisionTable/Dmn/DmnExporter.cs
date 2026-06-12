using System.Xml.Linq;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.RuleEngine.DecisionTable.Dmn;

/// <summary>
/// Exports a Muonroi <see cref="DecisionTableModel"/> to a DMN 1.3 compliant XML string.
/// </summary>
/// <remarks>
/// This is a new, spec-compliant implementation. It supersedes the incorrect element
/// nesting in <c>DecisionTableXmlSerializer.SerializeToDmnXml</c> which remains
/// available for backward compatibility but will be deprecated separately.
///
/// DMN 1.3 correct structure:
/// <code>
/// &lt;definitions xmlns="https://www.omg.org/spec/DMN/20191111/MODEL/" ...&gt;
///   &lt;decision id="..." name="..."&gt;
///     &lt;decisionTable id="dt_..." hitPolicy="..." [aggregation="..."]&gt;
///       &lt;input id="input_..." label="..."&gt;
///         &lt;inputExpression id="ie_..." typeRef="..."&gt;
///           &lt;text&gt;columnName&lt;/text&gt;
///         &lt;/inputExpression&gt;
///       &lt;/input&gt;
///       &lt;output id="output_..." name="..." label="..." typeRef="..." /&gt;
///       &lt;rule id="..."&gt;
///         &lt;description&gt;...&lt;/description&gt;
///         &lt;inputEntry id="ie_..."&gt;&lt;text&gt;expression&lt;/text&gt;&lt;/inputEntry&gt;
///         &lt;outputEntry id="oe_..."&gt;&lt;text&gt;expression&lt;/text&gt;&lt;/outputEntry&gt;
///       &lt;/rule&gt;
///     &lt;/decisionTable&gt;
///   &lt;/decision&gt;
/// &lt;/definitions&gt;
/// </code>
/// </remarks>
public static class DmnExporter
{
    private static readonly XNamespace Ns = DmnConstants.DmnNamespace;

    /// <summary>
    /// Exports a decision table to a DMN 1.3 XML string.
    /// </summary>
    /// <param name="table">The decision table model to export.</param>
    /// <returns>A well-formed DMN 1.3 XML string with UTF-8 XML declaration.</returns>
    public static string ExportToDmnXml(DecisionTableModel table)
    {
        MGuard.NotNull(table);

        (string hitPolicyStr, string? aggregation) = DmnHitPolicyMapper.ToDmnString(table.HitPolicy);

        // Build <decisionTable> element
        XElement decisionTable = new(Ns + DmnConstants.DecisionTable,
            new XAttribute(DmnConstants.AttrId, $"dt_{table.Id}"),
            new XAttribute(DmnConstants.AttrHitPolicy, hitPolicyStr));

        if (aggregation is not null)
        {
            decisionTable.Add(new XAttribute(DmnConstants.AttrAggregation, aggregation));
        }

        // Add one <input> per input column
        int inputIdx = 0;
        foreach (DecisionTableColumn col in table.InputColumns)
        {
            decisionTable.Add(BuildInputElement(col, inputIdx));
            inputIdx++;
        }

        // Add one <output> per output column
        int outputIdx = 0;
        foreach (DecisionTableColumn col in table.OutputColumns)
        {
            decisionTable.Add(BuildOutputElement(col, outputIdx));
            outputIdx++;
        }

        // Add one <rule> per row (ordered)
        foreach (DecisionTableRow row in table.Rows.OrderBy(r => r.Order))
        {
            decisionTable.Add(BuildRuleElement(row));
        }

        // Build <decision> element
        XElement decision = new(Ns + DmnConstants.Decision,
            new XAttribute(DmnConstants.AttrId, table.Id),
            new XAttribute(DmnConstants.AttrName, table.Name ?? string.Empty),
            decisionTable);

        // Build root <definitions> element
        XElement definitions = new(Ns + DmnConstants.Definitions,
            new XAttribute(DmnConstants.AttrId, $"def_{table.Id}"),
            new XAttribute(DmnConstants.AttrName, table.Name ?? string.Empty),
            new XAttribute(DmnConstants.AttrNamespace, DmnConstants.MuonroiDmnNamespace),
            decision);

        XDocument doc = new(
            new XDeclaration("1.0", "utf-8", "yes"),
            definitions);

        return doc.ToString();
    }

    // ── Private builders ────────────────────────────────────────────────────

    private static XElement BuildInputElement(DecisionTableColumn col, int index)
    {
        return new XElement(Ns + DmnConstants.Input,
            new XAttribute(DmnConstants.AttrId, $"input_{col.Id}"),
            new XAttribute(DmnConstants.AttrLabel, col.Label ?? col.Name ?? string.Empty),
            new XElement(Ns + DmnConstants.InputExpression,
                new XAttribute(DmnConstants.AttrId, $"ie_{col.Id}"),
                new XAttribute(DmnConstants.AttrTypeRef, col.DataType ?? "string"),
                new XElement(Ns + DmnConstants.Text, col.Name ?? string.Empty)));
    }

    private static XElement BuildOutputElement(DecisionTableColumn col, int index)
    {
        return new XElement(Ns + DmnConstants.Output,
            new XAttribute(DmnConstants.AttrId, $"output_{col.Id}"),
            new XAttribute(DmnConstants.AttrName, col.Name ?? string.Empty),
            new XAttribute(DmnConstants.AttrLabel, col.Label ?? col.Name ?? string.Empty),
            new XAttribute(DmnConstants.AttrTypeRef, col.DataType ?? "string"));
    }

    private static XElement BuildRuleElement(DecisionTableRow row)
    {
        XElement rule = new(Ns + DmnConstants.Rule,
            new XAttribute(DmnConstants.AttrId, row.Id));

        if (!string.IsNullOrEmpty(row.Description))
        {
            rule.Add(new XElement(Ns + DmnConstants.Description, row.Description));
        }

        int inputIdx = 0;
        foreach (DecisionTableCell cell in row.InputCells)
        {
            rule.Add(new XElement(Ns + DmnConstants.InputEntry,
                new XAttribute(DmnConstants.AttrId, $"ie_{row.Id}_{inputIdx}"),
                new XElement(Ns + DmnConstants.Text, cell.Expression ?? string.Empty)));
            inputIdx++;
        }

        int outputIdx = 0;
        foreach (DecisionTableCell cell in row.OutputCells)
        {
            rule.Add(new XElement(Ns + DmnConstants.OutputEntry,
                new XAttribute(DmnConstants.AttrId, $"oe_{row.Id}_{outputIdx}"),
                new XElement(Ns + DmnConstants.Text, cell.Expression ?? string.Empty)));
            outputIdx++;
        }

        return rule;
    }
}
