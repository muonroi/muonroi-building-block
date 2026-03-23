using System.Text.Json;
using FluentAssertions;
using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Commands;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class SplitCommandTests
{
    [Fact]
    public async Task RunAsync_WithFullyQualifiedClassFilter_AndInvalidVersion_FallsBackToOne()
    {
        string root = CreateTempRoot();
        try
        {
            string sourceDir = Path.Combine(root, "src");
            string outputDir = Path.Combine(root, "split-output");
            string exportJson = Path.Combine(root, "split-export.json");
            Directory.CreateDirectory(sourceDir);

            File.WriteAllText(
                Path.Combine(sourceDir, "Handlers.cs"),
                """
                using Muonroi.RuleEngine.Abstractions;

                namespace Sample.App;

                public sealed class OrderHandler
                {
                    [MExtractAsRule("ORD-001", Order = 1)]
                    public bool CheckOrder(OrderContext context)
                    {
                        return context.Total > 0;
                    }
                }

                public sealed class InvoiceHandler
                {
                    [MExtractAsRule("INV-001", Order = 2)]
                    public bool CheckInvoice(OrderContext context)
                    {
                        return context.Total >= 10;
                    }
                }

                public sealed class OrderContext
                {
                    public decimal Total { get; set; }
                }
                """);

            CommandContext context = CreateContext(
                root,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "src",
                    ["output-dir"] = "split-output",
                    ["export-json"] = "split-export.json",
                    ["class"] = "Sample.App.InvoiceHandler",
                    ["workflow"] = "invoice-workflow",
                    ["version"] = "abc",
                    ["parallel"] = "false"
                });

            int exitCode = await SplitCommand.RunAsync(context);

            exitCode.Should().Be(0);
            Directory.GetFiles(outputDir, "*.g.cs", SearchOption.TopDirectoryOnly)
                .Should().ContainSingle()
                .Which.Should().EndWith("INV_001.g.cs");

            using JsonDocument json = JsonDocument.Parse(await File.ReadAllTextAsync(exportJson));
            JsonElement rootElement = json.RootElement;
            rootElement.GetProperty("version").GetInt32().Should().Be(1);
            rootElement.GetProperty("rules").GetArrayLength().Should().Be(1);
            rootElement.GetProperty("rules")[0].GetProperty("code").GetString().Should().Be("INV-001");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task RunAsync_WithoutExtractableRules_ReturnsZero_AndCreatesNoFiles()
    {
        string root = CreateTempRoot();
        try
        {
            string sourceDir = Path.Combine(root, "src");
            string outputDir = Path.Combine(root, "split-output");
            Directory.CreateDirectory(sourceDir);

            File.WriteAllText(
                Path.Combine(sourceDir, "Handlers.cs"),
                """
                namespace Sample.App;

                public sealed class OrderHandler
                {
                    public bool CheckOrder(OrderContext context)
                    {
                        return context.Total > 0;
                    }
                }

                public sealed class OrderContext
                {
                    public decimal Total { get; set; }
                }
                """);

            CommandContext context = CreateContext(
                root,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "src",
                    ["output-dir"] = "split-output",
                    ["parallel"] = "false"
                });

            int exitCode = await SplitCommand.RunAsync(context);

            exitCode.Should().Be(0);
            Directory.Exists(outputDir).Should().BeFalse();
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static CommandContext CreateContext(string workingDirectory, IReadOnlyDictionary<string, string> options)
    {
        return new CommandContext
        {
            RawOptions = options,
            Config = new RuleGenConfig(),
            WorkingDirectory = workingDirectory
        };
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-split-tests", Guid.NewGuid().ToString("N"));
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
