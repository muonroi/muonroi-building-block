using System.Text;
using System.Xml;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Export;

/// <summary>
/// Generates TRX (Visual Studio Test Results) format from proliferation scenario results.
/// Compatible with Azure DevOps, Visual Studio, and MSTest reporting pipelines.
/// </summary>
public static class TrxWriter
{
    private const string TrxNamespace = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    /// <summary>
    /// Writes TRX report for the given workflow scenarios and results.
    /// </summary>
    public static string Write(
        string workflowName,
        IReadOnlyList<NeuronScenario> scenarios,
        IReadOnlyList<ScenarioResult> results,
        DateTimeOffset generatedAt)
    {
        // Build lookup
        Dictionary<string, ScenarioResult> resultMap = results
            .GroupBy(r => r.ScenarioId)
            .ToDictionary(g => g.Key, g => g.Last());

        // Pre-assign GUIDs for test definitions (must match UnitTestResult references)
        Dictionary<string, string> testGuids = scenarios
            .ToDictionary(s => s.Id, _ => Guid.NewGuid().ToString("D"));
        Dictionary<string, string> executionGuids = scenarios
            .ToDictionary(s => s.Id, _ => Guid.NewGuid().ToString("D"));

        int passed = 0;
        int failed = 0;

        foreach (NeuronScenario scenario in scenarios)
        {
            resultMap.TryGetValue(scenario.Id, out ScenarioResult? result);
            string outcome = MapOutcome(scenario, result);
            if (outcome == "Passed") passed++;
            else if (outcome == "Failed") failed++;
        }

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

            xml.WriteStartElement("TestRun", TrxNamespace);
            xml.WriteAttributeString("id", Guid.NewGuid().ToString("D"));
            xml.WriteAttributeString("name", $"{workflowName} @ {generatedAt:yyyy-MM-dd HH:mm:ss}");

            // Times
            xml.WriteStartElement("Times", TrxNamespace);
            xml.WriteAttributeString("creation", generatedAt.ToString("o"));
            xml.WriteAttributeString("queuing", generatedAt.ToString("o"));
            xml.WriteAttributeString("start", generatedAt.ToString("o"));
            xml.WriteAttributeString("finish", generatedAt.ToString("o"));
            xml.WriteEndElement(); // Times

            // Results
            xml.WriteStartElement("Results", TrxNamespace);
            foreach (NeuronScenario scenario in scenarios)
            {
                resultMap.TryGetValue(scenario.Id, out ScenarioResult? result);
                string outcome = MapOutcome(scenario, result);
                TimeSpan duration = result?.Duration ?? TimeSpan.Zero;

                xml.WriteStartElement("UnitTestResult", TrxNamespace);
                xml.WriteAttributeString("executionId", executionGuids[scenario.Id]);
                xml.WriteAttributeString("testId", testGuids[scenario.Id]);
                xml.WriteAttributeString("testName", scenario.ScenarioName);
                xml.WriteAttributeString("computerName", Environment.MachineName);
                xml.WriteAttributeString("duration", duration.ToString(@"hh\:mm\:ss\.fff"));
                xml.WriteAttributeString("startTime", generatedAt.ToString("o"));
                xml.WriteAttributeString("endTime", (generatedAt + duration).ToString("o"));
                xml.WriteAttributeString("testType", "13cdc9d9-ddb5-4fa4-a97d-d965ccfc6d4b"); // UnitTest GUID
                xml.WriteAttributeString("outcome", outcome);
                xml.WriteAttributeString("testListId", "8c84fa94-04c1-424b-9868-57a2d4851a1d"); // Default list

                if (outcome == "Failed" && result is not null)
                {
                    xml.WriteStartElement("Output", TrxNamespace);
                    xml.WriteStartElement("ErrorInfo", TrxNamespace);

                    xml.WriteStartElement("Message", TrxNamespace);
                    string errorMessage = result.Errors.Count > 0
                        ? string.Join("\n", result.Errors)
                        : "Scenario failed without specific error messages.";
                    xml.WriteValue(errorMessage);
                    xml.WriteEndElement(); // Message

                    if (!string.IsNullOrWhiteSpace(result.ActualBehavior))
                    {
                        xml.WriteStartElement("StackTrace", TrxNamespace);
                        xml.WriteValue(result.ActualBehavior);
                        xml.WriteEndElement(); // StackTrace
                    }

                    xml.WriteEndElement(); // ErrorInfo
                    xml.WriteEndElement(); // Output
                }

                xml.WriteEndElement(); // UnitTestResult
            }
            xml.WriteEndElement(); // Results

            // TestDefinitions
            xml.WriteStartElement("TestDefinitions", TrxNamespace);
            foreach (NeuronScenario scenario in scenarios)
            {
                xml.WriteStartElement("UnitTest", TrxNamespace);
                xml.WriteAttributeString("name", scenario.ScenarioName);
                xml.WriteAttributeString("id", testGuids[scenario.Id]);

                xml.WriteStartElement("Execution", TrxNamespace);
                xml.WriteAttributeString("id", executionGuids[scenario.Id]);
                xml.WriteEndElement(); // Execution

                xml.WriteStartElement("TestMethod", TrxNamespace);
                xml.WriteAttributeString("codeBase", workflowName);
                xml.WriteAttributeString("className", scenario.SeedRuleCode);
                xml.WriteAttributeString("name", scenario.ScenarioName);
                xml.WriteEndElement(); // TestMethod

                xml.WriteEndElement(); // UnitTest
            }
            xml.WriteEndElement(); // TestDefinitions

            // TestLists
            xml.WriteStartElement("TestLists", TrxNamespace);
            xml.WriteStartElement("TestList", TrxNamespace);
            xml.WriteAttributeString("name", "All Loaded Results");
            xml.WriteAttributeString("id", "19431567-8539-422a-85d7-44ee4e166bda");
            xml.WriteEndElement(); // TestList
            xml.WriteStartElement("TestList", TrxNamespace);
            xml.WriteAttributeString("name", workflowName);
            xml.WriteAttributeString("id", "8c84fa94-04c1-424b-9868-57a2d4851a1d");
            xml.WriteEndElement(); // TestList
            xml.WriteEndElement(); // TestLists

            // ResultSummary
            xml.WriteStartElement("ResultSummary", TrxNamespace);
            xml.WriteAttributeString("outcome", failed == 0 ? "Completed" : "Failed");
            xml.WriteStartElement("Counters", TrxNamespace);
            xml.WriteAttributeString("total", scenarios.Count.ToString());
            xml.WriteAttributeString("executed", scenarios.Count.ToString());
            xml.WriteAttributeString("passed", passed.ToString());
            xml.WriteAttributeString("failed", failed.ToString());
            xml.WriteEndElement(); // Counters
            xml.WriteEndElement(); // ResultSummary

            xml.WriteEndElement(); // TestRun
            xml.WriteEndDocument();
        }

        return sb.ToString();
    }

    private static string MapOutcome(NeuronScenario scenario, ScenarioResult? result)
    {
        if (result is null) return "NotExecuted";
        if (result.IsSuccess && result.MatchesExpectation) return "Passed";
        if (scenario.Status == ScenarioStatus.Skipped) return "NotExecuted";
        return "Failed";
    }
}
