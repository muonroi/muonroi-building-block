using Muonroi.RuleGen.Models;
using System.Text;

namespace Muonroi.RuleGen.Writers;

internal static class TestScaffoldWriter
{
    public static string Render(DiscoveredRuleType ruleType, string testNamespace)
    {
        StringBuilder sb = new();
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Muonroi.RuleEngine.Abstractions;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine($"namespace {testNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public class {ruleType.ClassName}Tests");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {ruleType.ClassName} _rule = new();");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public async Task EvaluateAsync_WhenCalled_ReturnsRuleResult()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var ctx = new {ruleType.ContextType}();");
        sb.AppendLine("        var facts = new FactBag();");
        sb.AppendLine();
        sb.AppendLine("        var result = await _rule.EvaluateAsync(ctx, facts, CancellationToken.None);");
        sb.AppendLine();
        sb.AppendLine("        Assert.NotNull(result);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
