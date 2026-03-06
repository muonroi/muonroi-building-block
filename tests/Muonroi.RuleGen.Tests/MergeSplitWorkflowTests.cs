using System.Text.Json;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public class MergeSplitWorkflowTests
{
    [Fact]
    public async Task Merge_WithCompileCheckFailure_ShouldRollbackTargetAndGeneratedFile()
    {
        string handler = """
using Muonroi.RuleEngine.Abstractions;

namespace Sample.App;

public class OrderHandler
{
}

public sealed class OrderContext
{
    public int Id { get; set; }
}
""";

        string runtimeJson = """
{
  "workflowName": "order-validation",
  "version": 1,
  "rules": [
    {
      "code": "VAL-FAIL-001",
      "name": "ValidateMissingPath",
      "order": 10,
      "hookPoint": "BeforeRule",
      "condition": "missing.value > 0",
      "action": "facts['ok'] = true"
    }
  ]
}
""";

        using TempProjectContext project = TempProjectContext.Create(handler, runtimeJson);
        string originalTarget = File.ReadAllText(project.HandlerPath);

        CliRunResult result = await CliProcess.RunAsync(
            "merge",
            "--rules-json", project.RuntimeRulePath,
            "--target", project.HandlerPath,
            "--class", "OrderHandler",
            "--context", "Sample.App.OrderContext",
            "--partial-class", "true",
            "--compile-check", "true",
            "--compile-target", project.ProjectPath,
            "--strategy", "replace");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Compile check failed", result.Combined, StringComparison.OrdinalIgnoreCase);

        string currentTarget = File.ReadAllText(project.HandlerPath);
        Assert.Equal(originalTarget, currentTarget);

        string generated = project.PathInRoot("src\\OrderHandler.Generated.cs");
        Assert.False(File.Exists(generated));
    }

    [Fact]
    public async Task Merge_WithClassTargeting_ShouldGenerateForSelectedClassOnly()
    {
        string handler = """
using Muonroi.RuleEngine.Abstractions;

namespace Sample.App;

public class OrderHandler
{
}

public class InvoiceHandler
{
}

public sealed class OrderContext
{
    public int Total { get; set; }
}
""";

        string runtimeJson = """
{
  "workflowName": "invoice-validation",
  "version": 1,
  "rules": [
    {
      "code": "INV-001",
      "name": "ValidateInvoice",
      "order": 10,
      "hookPoint": "BeforeRule",
      "condition": "true",
      "action": "facts['invoiceValid'] = true"
    }
  ]
}
""";

        using TempProjectContext project = TempProjectContext.Create(handler, runtimeJson);

        CliRunResult result = await CliProcess.RunAsync(
            "merge",
            "--rules-json", project.RuntimeRulePath,
            "--target", project.HandlerPath,
            "--class", "InvoiceHandler",
            "--context", "Sample.App.OrderContext",
            "--partial-class", "true",
            "--compile-check", "false",
            "--strategy", "replace");

        Assert.Equal(0, result.ExitCode);

        string invoiceGenerated = project.PathInRoot("src\\InvoiceHandler.Generated.cs");
        string orderGenerated = project.PathInRoot("src\\OrderHandler.Generated.cs");

        Assert.True(File.Exists(invoiceGenerated));
        Assert.False(File.Exists(orderGenerated));

        string generatedText = File.ReadAllText(invoiceGenerated);
        Assert.Contains("public partial class InvoiceHandler", generatedText);
        Assert.Contains("MExtractAsRule(\"INV-001\"", generatedText);
    }

    [Fact]
    public async Task Split_WithClassTargeting_ShouldExportOnlySelectedClassRules()
    {
        string handler = """
using Muonroi.RuleEngine.Abstractions;

namespace Sample.App;

public class OrderHandler
{
    [MExtractAsRule("ORD-001", Order = 1)]
    public Task<RuleResult> CheckOrder(OrderContext ctx, FactBag facts, CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }
}

public class InvoiceHandler
{
    [MExtractAsRule("INV-001", Order = 2)]
    public Task<RuleResult> CheckInvoice(OrderContext ctx, FactBag facts, CancellationToken ct)
    {
        facts["invoice"] = true;
        return Task.FromResult(RuleResult.Passed());
    }
}

public sealed class OrderContext
{
    public int Value { get; set; }
}
""";

        string runtimeJson = """
{
  "workflowName": "placeholder",
  "version": 1,
  "rules": []
}
""";

        using TempProjectContext project = TempProjectContext.Create(handler, runtimeJson);
        string outputDir = project.PathInRoot("split-output");
        string exportJson = project.PathInRoot("split-export.json");

        CliRunResult result = await CliProcess.RunAsync(
            "split",
            "--source", project.PathInRoot("src"),
            "--output-dir", outputDir,
            "--export-json", exportJson,
            "--class", "InvoiceHandler",
            "--workflow", "invoice-workflow",
            "--version", "5",
            "--context", "Sample.App.OrderContext");

        Assert.Equal(0, result.ExitCode);

        string[] generatedFiles = Directory.GetFiles(outputDir, "*.g.cs", SearchOption.TopDirectoryOnly);
        Assert.Single(generatedFiles);
        Assert.EndsWith("INV_001.g.cs", generatedFiles[0], StringComparison.OrdinalIgnoreCase);

        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(exportJson));
        JsonElement root = json.RootElement;

        Assert.Equal(5, root.GetProperty("version").GetInt32());
        JsonElement rules = root.GetProperty("rules");
        Assert.Equal(1, rules.GetArrayLength());
        Assert.Equal("INV-001", rules[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Merge_WithCompileCheckSuccess_ShouldPersistGeneratedChanges()
    {
        string handler = """
using Muonroi.RuleEngine.Abstractions;

namespace Sample.App;

public class OrderHandler
{
}

public sealed class OrderContext
{
    public OrderInfo Order { get; set; } = new();
    public CustomerInfo Customer { get; set; } = new();
}

public sealed class OrderInfo
{
    public decimal Total { get; set; }
}

public sealed class CustomerInfo
{
    public string VipStatus { get; set; } = string.Empty;
}
""";

        string runtimeJson = """
{
  "workflowName": "order-validation",
  "version": 3,
  "rules": [
    {
      "code": "VAL-OK-001",
      "name": "ValidatePremium",
      "order": 10,
      "hookPoint": "BeforeRule",
      "condition": "order.total > 100 and customer.vipStatus = 'Gold'",
      "action": "facts['discount'] = 0.2"
    }
  ]
}
""";

        using TempProjectContext project = TempProjectContext.Create(handler, runtimeJson);

        CliRunResult result = await CliProcess.RunAsync(
            "merge",
            "--rules-json", project.RuntimeRulePath,
            "--target", project.HandlerPath,
            "--class", "OrderHandler",
            "--context", "Sample.App.OrderContext",
            "--partial-class", "true",
            "--compile-check", "true",
            "--compile-target", project.ProjectPath,
            "--strategy", "replace");

        Assert.Equal(0, result.ExitCode);

        string generated = project.PathInRoot("src\\OrderHandler.Generated.cs");
        Assert.True(File.Exists(generated));

        string targetText = File.ReadAllText(project.HandlerPath);
        Assert.Contains("partial class OrderHandler", targetText);

        string generatedText = File.ReadAllText(generated);
        Assert.Contains("MExtractAsRule(\"VAL-OK-001\"", generatedText);
        Assert.Contains("ctx.Order.Total > 100", generatedText);
    }
}
