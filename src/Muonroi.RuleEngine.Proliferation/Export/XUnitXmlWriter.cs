using System.Text;
using System.Xml;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Export;

/// <summary>
/// Generates xUnit v2 XML test report from proliferation scenario results.
/// Compatible with GitHub Actions, Azure DevOps, Jenkins test result publishing.
/// </summary>
public static class XUnitXmlWriter
{
    /// <summary>
    /// Writes xUnit XML report for the given workflow scenarios and results.
    /// </summary>
    public static string Write(
        string workflowName,
        IReadOnlyList<NeuronScenario> scenarios,
        IReadOnlyList<ScenarioResult> results,
        DateTimeOffset generatedAt)
    {
        // Build a lookup for results by scenarioId
        Dictionary<string, ScenarioResult> resultMap = results
            .GroupBy(r => r.ScenarioId)
            .ToDictionary(g => g.Key, g => g.Last());

        int passed = 0;
        int failed = 0;
        int skipped = 0;
        double totalSeconds = 0;

        // Pre-calculate counts
        foreach (NeuronScenario scenario in scenarios)
        {
            resultMap.TryGetValue(scenario.Id, out ScenarioResult? result);
            string xunitResult = MapStatus(scenario, result);
            switch (xunitResult)
            {
                case "Pass": passed++; break;
                case "Fail": failed++; break;
                case "Skip": skipped++; break;
            }
            totalSeconds += result?.Duration.TotalSeconds ?? 0;
        }

        int total = scenarios.Count;

        StringBuilder sb = new();
        XmlWriterSettings settings = new()
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false
        };

        using (XmlWriter xml = XmlWriter.Create(sb, settings))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("assemblies");

            xml.WriteStartElement("assembly");
            xml.WriteAttributeString("name", workflowName);
            xml.WriteAttributeString("total", total.ToString());
            xml.WriteAttributeString("passed", passed.ToString());
            xml.WriteAttributeString("failed", failed.ToString());
            xml.WriteAttributeString("skipped", skipped.ToString());
            xml.WriteAttributeString("time", totalSeconds.ToString("F6"));
            xml.WriteAttributeString("timestamp", generatedAt.ToString("o"));

            xml.WriteStartElement("collection");
            xml.WriteAttributeString("name", workflowName);
            xml.WriteAttributeString("total", total.ToString());
            xml.WriteAttributeString("passed", passed.ToString());
            xml.WriteAttributeString("failed", failed.ToString());
            xml.WriteAttributeString("skipped", skipped.ToString());
            xml.WriteAttributeString("time", totalSeconds.ToString("F6"));

            foreach (NeuronScenario scenario in scenarios)
            {
                resultMap.TryGetValue(scenario.Id, out ScenarioResult? result);
                string xunitResult = MapStatus(scenario, result);
                double durationSeconds = result?.Duration.TotalSeconds ?? 0;

                xml.WriteStartElement("test");
                xml.WriteAttributeString("name", scenario.ScenarioName);
                xml.WriteAttributeString("type", scenario.SeedRuleCode);
                xml.WriteAttributeString("method", scenario.ScenarioName);
                xml.WriteAttributeString("time", durationSeconds.ToString("F6"));
                xml.WriteAttributeString("result", xunitResult);

                if (xunitResult == "Fail" && result is not null)
                {
                    xml.WriteStartElement("failure");
                    xml.WriteAttributeString("exception-type", scenario.ExpectedBehavior ?? "AssertionFailure");

                    string errorMessage = result.Errors.Count > 0
                        ? string.Join("\n", result.Errors)
                        : "Scenario failed without specific error messages.";

                    xml.WriteStartElement("message");
                    xml.WriteCData(errorMessage);
                    xml.WriteEndElement(); // message

                    if (!string.IsNullOrWhiteSpace(result.ActualBehavior))
                    {
                        xml.WriteStartElement("stack-trace");
                        xml.WriteCData(result.ActualBehavior);
                        xml.WriteEndElement(); // stack-trace
                    }

                    xml.WriteEndElement(); // failure
                }

                xml.WriteEndElement(); // test
            }

            xml.WriteEndElement(); // collection
            xml.WriteEndElement(); // assembly
            xml.WriteEndElement(); // assemblies
            xml.WriteEndDocument();
        }

        return sb.ToString();
    }

    private static string MapStatus(NeuronScenario scenario, ScenarioResult? result)
    {
        if (result is null)
        {
            return scenario.Status == ScenarioStatus.Skipped ? "Skip" : "Skip";
        }

        if (result.IsSuccess && result.MatchesExpectation) return "Pass";
        if (scenario.Status == ScenarioStatus.Skipped) return "Skip";
        return "Fail";
    }
}
