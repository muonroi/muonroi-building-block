using FluentAssertions;
using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Commands;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class VerifyCommandTests
{
    [Fact]
    public async Task RunAsync_WithMissingGeneratedFile_ReturnsTwo()
    {
        string root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "OrderHandler.cs"), CreateHandlerSource());
            Directory.CreateDirectory(Path.Combine(root, "Generated"));

            CommandContext context = CreateContext(
                root,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "OrderHandler.cs",
                    ["rules"] = "Generated",
                    ["namespace"] = "Demo.Generated",
                    ["parallel"] = "false"
                });

            int exitCode = await VerifyCommand.RunAsync(context);

            exitCode.Should().Be(2);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task RunAsync_WhenExpectedGeneratedFileExists_ReturnsZero()
    {
        string root = CreateTempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "OrderHandler.cs"), CreateHandlerSource());
            string generatedDir = Path.Combine(root, "Generated");
            Directory.CreateDirectory(generatedDir);
            File.WriteAllText(Path.Combine(generatedDir, "ORDER_VALIDATE.g.cs"), "// generated");

            CommandContext context = CreateContext(
                root,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = "OrderHandler.cs",
                    ["rules"] = "Generated",
                    ["namespace"] = "Demo.Generated",
                    ["parallel"] = "false"
                });

            int exitCode = await VerifyCommand.RunAsync(context);

            exitCode.Should().Be(0);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateHandlerSource()
    {
        return """
               using Muonroi.RuleEngine.Abstractions;

               namespace Demo.Handlers;

               public sealed class OrderHandler
               {
                   [MExtractAsRule("ORDER_VALIDATE", Order = 1)]
                   public bool Validate(OrderContext context)
                   {
                       return context.Total > 0;
                   }
               }

               public sealed class OrderContext
               {
                   public decimal Total { get; set; }
               }
               """;
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
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-verify-tests", Guid.NewGuid().ToString("N"));
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
