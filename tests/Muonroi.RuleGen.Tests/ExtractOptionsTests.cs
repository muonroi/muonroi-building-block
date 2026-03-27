using FluentAssertions;
using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Commands;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class ExtractOptionsTests
{
    [Fact]
    public void FromContext_WithSourceFile_InfersOutput_And_Namespace_From_Source()
    {
        string root = CreateTempRoot();
        try
        {
            string sourceDir = Path.Combine(root, "src", "Handlers");
            Directory.CreateDirectory(sourceDir);
            File.WriteAllText(
                Path.Combine(sourceDir, "OrderHandler.cs"),
                """
                namespace Demo.App.Handlers;

                public sealed class OrderHandler {}
                """);

            CommandContext context = new()
            {
                RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["source"] = Path.Combine("src", "Handlers", "OrderHandler.cs")
                },
                Config = new RuleGenConfig(),
                WorkingDirectory = root
            };

            ExtractCommand.ExtractOptions options = ExtractCommand.ExtractOptions.FromContext(context);

            options.Output.Should().Be(Path.Combine(root, "src", "Handlers", "Rules"));
            options.Namespace.Should().Be("Demo.App.Handlers.Rules");
            options.AutoRegister.Should().BeTrue();
            options.GenerateDispatchers.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void FromContext_WithProject_UsesGeneratedRulesOutput_And_ConfigFallbacks()
    {
        string root = CreateTempRoot();
        try
        {
            string projectDir = Path.Combine(root, "src", "Demo.App");
            Directory.CreateDirectory(projectDir);
            File.WriteAllText(Path.Combine(projectDir, "Demo.App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(
                Path.Combine(projectDir, "Sample.cs"),
                """
                namespace Demo.App.Features;

                public sealed class Marker {}
                """);

            CommandContext context = new()
            {
                RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["project"] = Path.Combine("src", "Demo.App", "Demo.App.csproj"),
                    ["generate-dispatchers"] = "false"
                },
                Config = new RuleGenConfig
                {
                    Extract = new RuleGenExtractConfig
                    {
                        Namespace = "Configured.Generated",
                        RegistrationClassName = "CustomRegistration",
                        DispatcherOverwrite = false,
                        WorkflowName = "configured-workflow"
                    }
                },
                WorkingDirectory = root
            };

            ExtractCommand.ExtractOptions options = ExtractCommand.ExtractOptions.FromContext(context);

            options.Output.Should().Be(Path.Combine(projectDir, "Generated", "Rules"));
            options.Namespace.Should().Be("Demo.App.Features.Generated.Rules");
            options.RegistrationClassName.Should().Be("CustomRegistration");
            options.GenerateDispatchers.Should().BeFalse();
            options.DispatcherOverwrite.Should().BeFalse();
            options.WorkflowName.Should().Be("configured-workflow");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void FromContext_WithoutResolvableLocation_Throws()
    {
        CommandContext context = new()
        {
            RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Config = new RuleGenConfig(),
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        Action act = () => ExtractCommand.ExtractOptions.FromContext(context);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Missing output location*");
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-extract-options", Guid.NewGuid().ToString("N"));
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
