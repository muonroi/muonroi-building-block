using Muonroi.RuleGen.Models;
using Muonroi.RuleGen.Writers;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public class DispatcherWriterTests
{
    [Fact]
    public void Render_WithoutWorkflowName_GeneratesCodeFirstDispatcher()
    {
        var context = new DiscoveredDispatcherContext(
            "MyContext", "IMyDispatcher", "MyDispatcher", "MyDispatcher.g.cs");

        string output = DispatcherWriter.Render("TestNs", context);

        Assert.Contains("RuleOrchestrator<MyContext> orchestrator", output);
        Assert.Contains("orchestrator.ExecuteAsync(context", output);
        Assert.Contains("orchestrator.ExecuteWithResultAsync(", output);
        Assert.DoesNotContain("RulesEngineService", output);
        Assert.DoesNotContain("MWorkflowName", output);
    }

    [Fact]
    public void Render_WithWorkflowName_GeneratesVersionAwareDispatcher()
    {
        var context = new DiscoveredDispatcherContext(
            "MyContext", "IMyDispatcher", "MyDispatcher", "MyDispatcher.g.cs",
            WorkflowName: "MY_WORKFLOW");

        string output = DispatcherWriter.Render("TestNs", context);

        // Has RulesEngineService optional parameter
        Assert.Contains("RulesEngineService? rulesEngineService = null", output);

        // Has workflow name constant
        Assert.Contains("MWorkflowName = \"MY_WORKFLOW\"", output);

        // Has version-aware delegation
        Assert.Contains(".ExecuteWithResultAsync(MWorkflowName, context, cancellationToken)", output);

        // Has fallback to orchestrator
        Assert.Contains("orchestrator.ExecuteAsync(context", output);
        Assert.Contains("orchestrator.ExecuteWithResultAsync(", output);

        // Has using for Runtime
        Assert.Contains("using Muonroi.RuleEngine.Runtime.Rules;", output);
    }

    [Fact]
    public void Render_InterfaceUnchanged_RegardlessOfWorkflowName()
    {
        var withoutWf = new DiscoveredDispatcherContext(
            "MyContext", "IMyDispatcher", "MyDispatcher", "MyDispatcher.g.cs");
        var withWf = new DiscoveredDispatcherContext(
            "MyContext", "IMyDispatcher", "MyDispatcher", "MyDispatcher.g.cs",
            WorkflowName: "TEST");

        string outputWithout = DispatcherWriter.Render("TestNs", withoutWf);
        string outputWith = DispatcherWriter.Render("TestNs", withWf);

        // Interface declaration must be identical
        const string interfaceDecl = "public interface IMyDispatcher";
        Assert.Contains(interfaceDecl, outputWithout);
        Assert.Contains(interfaceDecl, outputWith);

        // Both have ExecuteAsync and ExecuteWithResultAsync in interface
        Assert.Contains("Task<FactBag> ExecuteAsync(MyContext context", outputWithout);
        Assert.Contains("Task<FactBag> ExecuteAsync(MyContext context", outputWith);
        Assert.Contains("Task<OrchestratorResult> ExecuteWithResultAsync(", outputWithout);
        Assert.Contains("Task<OrchestratorResult> ExecuteWithResultAsync(", outputWith);
    }

    [Fact]
    public void BuildContexts_WithWorkflowName_PropagatesToContext()
    {
        var discovered = new List<DiscoveredRuleType>
        {
            new("MyNamespace.MyRule", "MyContext")
        };

        IReadOnlyList<DiscoveredDispatcherContext> contexts =
            DispatcherWriter.BuildContexts(discovered, "Dispatcher", "TEST_FLOW");

        Assert.Single(contexts);
        Assert.Equal("TEST_FLOW", contexts[0].WorkflowName);
    }

    [Fact]
    public void BuildContexts_WithoutWorkflowName_WorkflowNameIsNull()
    {
        var discovered = new List<DiscoveredRuleType>
        {
            new("MyNamespace.MyRule", "MyContext")
        };

        IReadOnlyList<DiscoveredDispatcherContext> contexts =
            DispatcherWriter.BuildContexts(discovered, "Dispatcher");

        Assert.Single(contexts);
        Assert.Null(contexts[0].WorkflowName);
    }
}
