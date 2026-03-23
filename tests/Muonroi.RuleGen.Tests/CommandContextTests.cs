using FluentAssertions;
using Muonroi.RuleGen.Cli;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class CommandContextTests
{
    private static readonly object CurrentDirectoryLock = new();

    [Fact]
    public void Create_WithExplicitConfigPath_ShouldLoadConfigAndSetWorkingDirectory()
    {
        string root = CreateTempRoot();
        try
        {
            string configPath = Path.Combine(root, "custom.rulegen.json");
            File.WriteAllText(configPath, """
                {
                  // comment should be ignored
                  "extract": {
                    "namespace": "Demo.Generated",
                    "generateTests": true,
                  }
                }
                """);

            CommandContext context;
            lock (CurrentDirectoryLock)
            {
                string previous = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(root);
                    context = CommandContext.Create(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["config"] = "custom.rulegen.json"
                    });
                }
                finally
                {
                    Directory.SetCurrentDirectory(previous);
                }
            }

            context.WorkingDirectory.Should().Be(root);
            context.Config.ConfigPath.Should().Be(configPath);
            context.Config.Extract.Namespace.Should().Be("Demo.Generated");
            context.Config.Extract.GenerateTests.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Create_WithoutExplicitConfig_ShouldUseDefaultRulegenFile()
    {
        string root = CreateTempRoot();
        try
        {
            string configPath = Path.Combine(root, ".rulegen.json");
            File.WriteAllText(configPath, """
                {
                  "conventions": {
                    "ruleCodePrefix": "ORD"
                  },
                  "validation": {
                    "requireXmlDocs": true
                  }
                }
                """);

            CommandContext context;
            lock (CurrentDirectoryLock)
            {
                string previous = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(root);
                    context = CommandContext.Create(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                }
                finally
                {
                    Directory.SetCurrentDirectory(previous);
                }
            }

            context.Config.ConfigPath.Should().Be(configPath);
            context.Config.Conventions.RuleCodePrefix.Should().Be("ORD");
            context.Config.Validation.RequireXmlDocs.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Create_WithoutConfigFile_ShouldReturnDefaults()
    {
        string root = CreateTempRoot();
        try
        {
            CommandContext context;
            lock (CurrentDirectoryLock)
            {
                string previous = Directory.GetCurrentDirectory();
                try
                {
                    Directory.SetCurrentDirectory(root);
                    context = CommandContext.Create(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                }
                finally
                {
                    Directory.SetCurrentDirectory(previous);
                }
            }

            context.Config.ConfigPath.Should().BeNull();
            context.Config.Extract.Validate.Should().BeTrue();
            context.Config.Extract.AutoRegister.Should().BeTrue();
            context.Config.Validation.DetectCycles.Should().BeTrue();
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-context-tests", Guid.NewGuid().ToString("N"));
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
