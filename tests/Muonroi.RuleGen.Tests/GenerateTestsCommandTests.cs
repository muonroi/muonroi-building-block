using FluentAssertions;
using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Commands;
using Xunit;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleGen.Tests;

public sealed class GenerateTestsCommandTests
{
    [Fact]
    public async Task RunAsync_WithMissingRequiredOptions_ShouldThrow()
    {
        CommandContext context = new()
        {
            RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Config = new RuleGenConfig(),
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        Func<Task> act = () => GenerateTestsCommand.RunAsync(context);

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*--rules*--output*");
    }

    [Fact]
    public async Task RunAsync_WithDiscoveredRules_ShouldGenerateScaffoldFiles()
    {
        string root = CreateTempRoot();
        try
        {
            string rulesDir = Path.Combine(root, "GeneratedRules");
            string outputDir = Path.Combine(root, "GeneratedTests");
            Directory.CreateDirectory(rulesDir);

            File.WriteAllText(Path.Combine(rulesDir, "OrderApprovalRule.cs"), """
                using System.Threading;
                using System.Threading.Tasks;
                using Muonroi.RuleEngine.Abstractions;

                namespace Demo.Rules;

                public sealed class OrderApprovalRule : IRule<OrderContext>
                {
                    public Task<RuleResult> EvaluateAsync(OrderContext context, FactBag facts, CancellationToken cancellationToken)
                    {
                        return Task.FromResult(RuleResult.Passed());
                    }
                }

                public sealed class OrderContext
                {
                }
                """);

            CommandContext context = new()
            {
                RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["rules"] = "GeneratedRules",
                    ["output"] = "GeneratedTests",
                    ["namespace"] = "Demo.RuleTests"
                },
                Config = new RuleGenConfig(),
                WorkingDirectory = root
            };

            int exitCode = await GenerateTestsCommand.RunAsync(context);

            exitCode.Should().Be(0);
            string generatedFile = Path.Combine(outputDir, "Demo.Rules.OrderApprovalRuleTests.cs");
            File.Exists(generatedFile).Should().BeTrue();

            string generated = await File.ReadAllTextAsync(generatedFile);
            generated.Should().Contain("namespace Demo.RuleTests.Tests;");
            generated.Should().Contain("public class Demo.Rules.OrderApprovalRuleTests");
            generated.Should().Contain("var ctx = new OrderContext();");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task RunAsync_WithoutNamespaceOption_ShouldUseConfigNamespaceFallback()
    {
        string root = CreateTempRoot();
        try
        {
            string rulesDir = Path.Combine(root, "Rules");
            string outputDir = Path.Combine(root, "Tests");
            Directory.CreateDirectory(rulesDir);

            File.WriteAllText(Path.Combine(rulesDir, "SampleRule.cs"), """
                using System.Threading;
                using System.Threading.Tasks;
                using Muonroi.RuleEngine.Abstractions;

                public sealed class SampleRule : Muonroi.RuleEngine.Abstractions.IRule<SampleContext>
                {
                    public Task<RuleResult> EvaluateAsync(SampleContext context, FactBag facts, CancellationToken cancellationToken)
                    {
                        return Task.FromResult(RuleResult.Passed());
                    }
                }

                public sealed class SampleContext
                {
                }
                """);

            CommandContext context = new()
            {
                RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["rules"] = "Rules",
                    ["output"] = "Tests"
                },
                Config = new RuleGenConfig
                {
                    Extract = new RuleGenExtractConfig
                    {
                        Namespace = "Configured.Generated"
                    }
                },
                WorkingDirectory = root
            };

            await GenerateTestsCommand.RunAsync(context);

            string generated = await File.ReadAllTextAsync(Path.Combine(outputDir, "SampleRuleTests.cs"));
            generated.Should().Contain("namespace Configured.Generated.Tests;");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-generate-tests", Guid.NewGuid().ToString("N"));
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
        }
    }
}
