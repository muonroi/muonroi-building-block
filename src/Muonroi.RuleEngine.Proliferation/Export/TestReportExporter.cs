using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Export;

/// <summary>
/// Facade for exporting proliferation results as standardized test report formats.
/// Dispatches to XUnitXmlWriter or TrxWriter based on format.
/// </summary>
public static class TestReportExporter
{
    /// <summary>
    /// Exports scenario results to a test report XML string in the specified format.
    /// </summary>
    /// <param name="workflowName">Name of the workflow (used as assembly/collection name).</param>
    /// <param name="scenarios">The neuron scenarios generated for this workflow.</param>
    /// <param name="results">Execution results for the scenarios.</param>
    /// <param name="format">Target report format (XUnitXml or Trx).</param>
    /// <param name="generatedAt">Timestamp for the report generation. Defaults to UtcNow.</param>
    /// <returns>XML string in the requested format.</returns>
    public static string Export(
        string workflowName,
        IReadOnlyList<NeuronScenario> scenarios,
        IReadOnlyList<ScenarioResult> results,
        TestReportFormat format,
        DateTimeOffset? generatedAt = null)
    {
        DateTimeOffset timestamp = generatedAt ?? DateTimeOffset.UtcNow;

        return format switch
        {
            TestReportFormat.XUnitXml => XUnitXmlWriter.Write(workflowName, scenarios, results, timestamp),
            TestReportFormat.Trx => TrxWriter.Write(workflowName, scenarios, results, timestamp),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, $"Unsupported report format: {format}")
        };
    }
}
