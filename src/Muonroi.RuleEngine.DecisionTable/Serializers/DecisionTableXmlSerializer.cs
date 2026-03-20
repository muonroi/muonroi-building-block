using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Serializers;

/// <summary>
/// Provides XML serialization for decision tables, specifically for DMN format.
/// </summary>
public sealed class DecisionTableXmlSerializer
{
    /// <summary>
    /// Serializes a decision table to a DMN-compliant XML string.
    /// </summary>
    /// <param name="table">The decision table to serialize.</param>
    /// <returns>A DMN XML string representation of the decision table.</returns>
    public static string SerializeToDmnXml(DecisionTableModel table)
    {
        XNamespace dmn = "https://www.omg.org/spec/DMN/20191111/MODEL/";

        XElement decision = new(dmn + "decision",
            new XAttribute("id", table.Id),
            new XAttribute("name", table.Name),
            new XElement(dmn + "description", table.Description),
            new XElement(dmn + "decisionTable",
                new XAttribute("hitPolicy", table.HitPolicy.ToString().ToUpperInvariant()),
                new XElement(dmn + "input",
                    table.InputColumns.Select(col =>
                        new XElement(dmn + "inputExpression",
                            new XAttribute("id", col.Id),
                            new XElement(dmn + "text", col.Name)))),
                new XElement(dmn + "output",
                    table.OutputColumns.Select(col =>
                        new XElement(dmn + "outputClause",
                            new XAttribute("id", col.Id),
                            new XAttribute("name", col.Name),
                            new XAttribute("label", col.Label)))),
                table.Rows.OrderBy(x => x.Order).Select(row =>
                    new XElement(dmn + "rule",
                        new XAttribute("id", row.Id),
                        row.InputCells.Select(cell => new XElement(dmn + "inputEntry", new XElement(dmn + "text", cell.Expression))),
                        row.OutputCells.Select(cell => new XElement(dmn + "outputEntry", new XElement(dmn + "text", cell.Expression)))))));

        XElement definitions = new(dmn + "definitions",
            new XAttribute("id", $"def_{table.Id}"),
            new XAttribute("name", table.Name),
            decision);

        XDocument doc = new(new XDeclaration("1.0", "utf-8", "yes"), definitions);
        return doc.ToString();
    }
}
