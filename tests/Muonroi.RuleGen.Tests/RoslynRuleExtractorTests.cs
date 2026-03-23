using FluentAssertions;
using Muonroi.RuleGen.Services;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class RoslynRuleExtractorTests
{
    [Fact]
    public async Task ExtractAsync_WithEmptySources_ReturnsEmpty()
    {
        IReadOnlyList<Muonroi.RuleGen.Models.ExtractedRuleDefinition> result = await RoslynRuleExtractor.ExtractAsync(
            [],
            "Demo.Generated",
            contextOverride: null,
            useParallel: false,
            CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_ExtractsRichDefinition_FromMethodBody()
    {
        string root = CreateTempRoot();
        try
        {
            string sourceFile = Path.Combine(root, "OrderHandler.cs");
            File.WriteAllText(sourceFile, CreateRichSource());

            IReadOnlyList<Muonroi.RuleGen.Models.ExtractedRuleDefinition> result = await RoslynRuleExtractor.ExtractAsync(
                [sourceFile],
                "Demo.Generated",
                contextOverride: null,
                useParallel: false,
                CancellationToken.None);

            result.Should().ContainSingle();
            Muonroi.RuleGen.Models.ExtractedRuleDefinition rule = result[0];

            rule.Code.Should().Be("Validate");
            rule.MethodName.Should().Be("ValidateAsync");
            rule.ClassName.Should().Be("OrderHandler");
            rule.SourceNamespace.Should().Be("Demo.Source");
            rule.OutputNamespace.Should().Be("Demo.Generated");
            rule.ContextType.Should().Be("OrderContext");
            rule.ReturnType.Should().Be("Task<RuleResult>");
            rule.Order.Should().Be(3);
            rule.HookPoint.Should().Be("AfterRule");
            rule.DependsOn.Should().Equal("PRECHECK", "Another");
            rule.UseFactBagAware.Should().BeTrue();
            rule.IsAsync.Should().BeTrue();
            rule.DocumentationComment.Should().Contain("<summary>");
            rule.DocumentationComment.Should().Contain("Validates the order.");
            rule.CustomAttributes.Should().Contain("[MyCustom]");
            rule.Usings.Should().Contain("using System.Threading.Tasks;");
            rule.Parameters.Should().Contain(x => x.Name == "fallback" && x.HasDefaultValue && x.DefaultValueExpression == "\"x\"");
            rule.HelperMethods.Should().HaveCount(2);
            rule.HelperMethods.Should().Contain(x => x.MethodName == "HelperAsync");
            rule.HelperMethods.Should().Contain(x => x.MethodName == "NestedAsync");
            rule.Dependencies.Should().ContainSingle();
            rule.Dependencies[0].FieldName.Should().Be("fooService");
            rule.Dependencies[0].TypeName.Should().Be("IFooService");
            rule.Dependencies[0].ConstructorParameterName.Should().Be("fooService");
            rule.MethodBody.Should().Contain("await HelperAsync();");
            rule.ExpressionBody.Should().BeNull();
            rule.SourceFile.Should().Be(sourceFile);
            rule.SourceLine.Should().BeGreaterThan(1);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ExtractAsync_WithParallelAndContextOverride_UsesOverride_AndOrdersByCode()
    {
        string root = CreateTempRoot();
        try
        {
            string firstFile = Path.Combine(root, "B.cs");
            string secondFile = Path.Combine(root, "A.cs");

            File.WriteAllText(
                firstFile,
                """
                namespace Demo.Source;

                public sealed class FactBag {}
                public sealed class HostB
                {
                    [ExtractAsRule("B-RULE", DependsOn = new[] { "X", "Y" })]
                    public bool Check(FactBag facts, CancellationToken ct) => true;
                }
                """);

            File.WriteAllText(
                secondFile,
                """
                namespace Demo.Source;

                public sealed class FactBag {}
                public sealed class HostA
                {
                    [ExtractAsRule("A-RULE")]
                    public bool Check(FactBag facts, CancellationToken ct) => true;
                }
                """);

            IReadOnlyList<Muonroi.RuleGen.Models.ExtractedRuleDefinition> result = await RoslynRuleExtractor.ExtractAsync(
                [firstFile, secondFile],
                "Demo.Generated",
                contextOverride: "CustomContext",
                useParallel: true,
                CancellationToken.None);

            result.Should().HaveCount(2);
            result.Select(x => x.Code).Should().Equal("A-RULE", "B-RULE");
            result.Should().OnlyContain(x => x.ContextType == "CustomContext");
            result.Should().OnlyContain(x => x.MethodBody == null);
            result.Should().OnlyContain(x => x.ExpressionBody == "true");
            result.Single(x => x.Code == "B-RULE").DependsOn.Should().Equal("X", "Y");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateRichSource()
    {
        return """
               using System;
               using System.Threading;
               using System.Threading.Tasks;

               namespace Demo.Source;

               public sealed class MyCustomAttribute : Attribute {}
               public interface IFooService {}
               public sealed class FactBag {}
               public sealed class RuleResult
               {
                   public static RuleResult Passed() => new();
                   public static RuleResult Failure(string message) => new();
               }

               public enum HookPoint
               {
                   BeforeRule,
                   AfterRule
               }

               public sealed class OrderContext {}

               public static class RuleCodes
               {
                   public const string Validate = "VALIDATE_ORDER";
                   public const string Another = "ANOTHER_RULE";
               }

               public sealed class OrderHandler(IFooService fooService)
               {
                   /// <summary>
                   /// Validates the order.
                   /// </summary>
                   [MyCustom]
                   [MExtractAsRule(nameof(RuleCodes.Validate), Order = 3, HookPoint = HookPoint.AfterRule, DependsOn = ["PRECHECK", nameof(RuleCodes.Another)], UseFactBagAware = true)]
                   public async Task<RuleResult> ValidateAsync(OrderContext context, FactBag facts, CancellationToken ct, string fallback = "x")
                   {
                       await HelperAsync();
                       return RuleResult.Passed();
                   }

                   private async Task HelperAsync()
                   {
                       await NestedAsync();
                       _ = fooService.ToString();
                   }

                   private static async Task NestedAsync()
                   {
                       await Task.Yield();
                   }
               }
               """;
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-roslyn-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup for temp test folders
        }
    }
}
