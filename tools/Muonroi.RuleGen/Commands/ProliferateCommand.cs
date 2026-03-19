using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Proliferation;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Export;
using Muonroi.RuleEngine.Proliferation.Models;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleGen.Cli;
using Spectre.Console;
using System.Text.Json;

namespace Muonroi.RuleGen.Commands;

/// <summary>
/// RuleGen CLI command: proliferate
/// Reads a ruleset JSON file, bootstraps the proliferation engine,
/// generates test scenarios, and exports a test report.
///
/// Usage:
///   muonroi-rule proliferate --workflow-name OrderApproval --ruleset-file ruleset.json --output report.xml --format xunit --max-scenarios 20
/// </summary>
internal static class ProliferateCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        string? workflowName = OptionReader.GetString(context, "workflow-name");
        string? rulesetFile = OptionReader.GetString(context, "ruleset-file");
        string output = OptionReader.GetString(context, "output") ?? "proliferation-report.xml";
        string format = OptionReader.GetString(context, "format") ?? "xunit";
        int maxScenarios = int.TryParse(OptionReader.GetString(context, "max-scenarios"), out int ms) ? ms : 20;

        if (string.IsNullOrWhiteSpace(workflowName))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --workflow-name is required.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(rulesetFile))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --ruleset-file is required.");
            return 1;
        }

        string rulesetPath = Path.GetFullPath(rulesetFile, context.WorkingDirectory);
        if (!File.Exists(rulesetPath))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Ruleset file not found: {rulesetPath}");
            return 1;
        }

        string ruleSetJson = await File.ReadAllTextAsync(rulesetPath);

        // Bootstrap DI
        ServiceCollection services = new();

        // Build minimal configuration (brain defaults to ollama, can override via env/config)
        IConfiguration configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables("MUONROI_")
            .Build();

        services.AddMProliferationEngine(configuration);

        // Register a file-based IRuleSetStore that serves the provided JSON
        services.AddSingleton<IRuleSetStore>(new FileRuleSetStore(workflowName, ruleSetJson));

        // Remove the hosted services (worker/auto-trigger) — not needed in CLI
        // We build a minimal provider for just the brain + store

        ServiceProvider sp = services.BuildServiceProvider();
        IRuleProliferationBrain brain = sp.GetRequiredService<IRuleProliferationBrain>();
        IProliferationStore store = sp.GetRequiredService<IProliferationStore>();

        AnsiConsole.MarkupLine($"[blue]Proliferating[/] '{workflowName}' with up to {maxScenarios} scenarios...");

        ProliferationContext ctx = new()
        {
            Scope = ProliferationScope.Workflow,
            CurrentDepth = 0,
            RemainingBudget = maxScenarios,
            FocusAreas = [],
            TenantId = null,
            RuleSetKind = RuleSetKindDetector.Detect(ruleSetJson)
        };

        ProliferationPlan plan = await brain.AnalyzeAsync(
            seedRuleCode: workflowName,
            ruleSetJson: ruleSetJson,
            executionResult: null,
            factBagSnapshot: null,
            context: ctx,
            ct: CancellationToken.None);

        await store.SaveScenariosAsync(plan.Scenarios, CancellationToken.None);

        AnsiConsole.MarkupLine($"[green]Generated[/] {plan.Scenarios.Count} scenarios using {plan.AiModelUsed} in {plan.GenerationDuration.TotalSeconds:F1}s.");

        // Export report
        TestReportFormat reportFormat = format.Equals("trx", StringComparison.OrdinalIgnoreCase)
            ? TestReportFormat.Trx
            : format.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? TestReportFormat.XUnitXml // fallback: xunit for JSON not supported, use xunit
                : TestReportFormat.XUnitXml;

        // No results yet (CLI doesn't execute — it generates scenarios for inspection)
        string report = TestReportExporter.Export(workflowName, plan.Scenarios, [], reportFormat);

        string outputPath = Path.GetFullPath(output, context.WorkingDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, report);

        AnsiConsole.MarkupLine($"[green]Report written[/] → {outputPath}");

        // Print summary table
        Table table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Scenario");
        table.AddColumn("Type");
        table.AddColumn("Scope");
        table.AddColumn("Depth");

        foreach (NeuronScenario scenario in plan.Scenarios.Take(20))
        {
            table.AddRow(
                scenario.ScenarioName,
                scenario.Type.ToString(),
                scenario.Scope.ToString(),
                scenario.GenerationDepth.ToString());
        }

        if (plan.Scenarios.Count > 20)
        {
            table.AddRow($"... and {plan.Scenarios.Count - 20} more", "", "", "");
        }

        AnsiConsole.Write(new Panel(table).Header($"[bold green]Proliferation Results — {workflowName}[/]"));

        return 0;
    }

    /// <summary>
    /// Simple IRuleSetStore that serves a single in-memory ruleset for CLI use.
    /// </summary>
    private sealed class FileRuleSetStore(string workflowName, string json) : IRuleSetStore
    {
        public Task<string?> GetAsync(string wf, int? version = null, CancellationToken ct = default)
            => Task.FromResult<string?>(
                string.Equals(wf, workflowName, StringComparison.OrdinalIgnoreCase) ? json : null);

        public Task SaveAsync(string wf, string j, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SetActiveVersionAsync(string wf, int version, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int[]> GetVersionsAsync(string wf, CancellationToken ct = default)
            => Task.FromResult(Array.Empty<int>());
    }
}
