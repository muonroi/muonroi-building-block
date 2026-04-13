using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Commands;
using FluentAssertions;
using Xunit;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleGen.Tests;

public sealed class RegisterCommandTests
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

        Func<Task> act = () => RegisterCommand.RunAsync(context);

        await act.Should().ThrowAsync<MInternalException>()
            .WithMessage("*--rules*");
    }

    [Fact]
    public async Task RunAsync_Generates_Registration_And_Dispatchers()
    {
        using TempDir temp = new();
        string rulesDir = temp.PathInRoot("GeneratedRules");
        string outputFile = temp.PathInRoot("Generated/MGeneratedRuleRegistrationExtensions.g.cs");
        string dispatcherDir = temp.PathInRoot("Generated/Dispatchers");
        Directory.CreateDirectory(rulesDir);
        File.WriteAllText(Path.Combine(rulesDir, "OrderApprovalRule.cs"), """
            using Muonroi.RuleEngine.Abstractions;

            namespace Demo.Rules;

            public sealed class OrderApprovalRule : IRule<OrderContext> { }
            public sealed class InvoiceRule : IRule<OrderContext> { }
            public sealed class OrderContext { }
            """);

        CommandContext context = new()
        {
            RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rules"] = "GeneratedRules",
                ["output"] = "Generated/MGeneratedRuleRegistrationExtensions.g.cs",
                ["namespace"] = "Demo.Generated",
                ["dispatcher-output"] = "Generated/Dispatchers",
                ["workflow-name"] = "ORDER_FLOW"
            },
            Config = new RuleGenConfig(),
            WorkingDirectory = temp.RootPath
        };

        int exitCode = await RegisterCommand.RunAsync(context);

        exitCode.Should().Be(0);
        File.Exists(outputFile).Should().BeTrue();
        string registration = await File.ReadAllTextAsync(outputFile);
        registration.Should().Contain("namespace Demo.Generated;");
        registration.Should().Contain("AddMGeneratedRules");
        registration.Should().Contain("OrderApprovalRule");
        registration.Should().Contain("InvoiceRule");
        registration.Should().Contain("TryAddScoped<IOrderGeneratedRuleEngineDispatcher, OrderGeneratedRuleEngineDispatcher>");

        string dispatcherFile = Path.Combine(dispatcherDir, "OrderGeneratedRuleEngineDispatcher.g.cs");
        File.Exists(dispatcherFile).Should().BeTrue();
        string dispatcher = await File.ReadAllTextAsync(dispatcherFile);
        dispatcher.Should().Contain("MWorkflowName = \"ORDER_FLOW\"");
    }

    [Fact]
    public async Task RunAsync_Skips_Existing_Dispatchers_When_Overwrite_Is_Disabled()
    {
        using TempDir temp = new();
        string rulesDir = temp.PathInRoot("Rules");
        string outputFile = temp.PathInRoot("Out/Registration.g.cs");
        string dispatcherDir = temp.PathInRoot("Out/Dispatchers");
        Directory.CreateDirectory(rulesDir);
        Directory.CreateDirectory(dispatcherDir);
        File.WriteAllText(Path.Combine(rulesDir, "OrderApprovalRule.cs"), """
            using Muonroi.RuleEngine.Abstractions;

            public sealed class OrderApprovalRule : IRule<OrderContext> { }
            public sealed class OrderContext { }
            """);
        string dispatcherFile = Path.Combine(dispatcherDir, "OrderContextGeneratedRuleEngineDispatcher.g.cs");
        await File.WriteAllTextAsync(dispatcherFile, "// existing dispatcher");

        CommandContext context = new()
        {
            RawOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rules"] = "Rules",
                ["output"] = "Out/Registration.g.cs",
                ["dispatcher-output"] = "Out/Dispatchers",
                ["dispatcher-overwrite"] = "false"
            },
            Config = new RuleGenConfig(),
            WorkingDirectory = temp.RootPath
        };

        int exitCode = await RegisterCommand.RunAsync(context);

        exitCode.Should().Be(0);
        (await File.ReadAllTextAsync(dispatcherFile)).Should().Be("// existing dispatcher");
        File.Exists(outputFile).Should().BeTrue();
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-register", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string PathInRoot(string relative)
        {
            return Path.Combine(RootPath, relative);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
