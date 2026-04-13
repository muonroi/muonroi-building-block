using Spectre.Console;

namespace Muonroi.RuleGen.Cli;

internal static class CommandLineWriter
{
    public static void PrintUsage()
    {
        AnsiConsole.Write(new FigletText("MUONROI RULEGEN").Color(Color.Blue));
        AnsiConsole.MarkupLine("[bold blue]Muonroi.RuleGen (muonroi-rule) v2.0.0[/]");
        AnsiConsole.MarkupLine("Code-first rule extraction CLI for Muonroi rule engine.\n");

        Table table = new Table().BorderStyle(Style.Parse("blue"));
        table.AddColumn("Command");
        table.AddColumn("Description");

        table.AddRow("[yellow]extract[/]", "Extract methods marked with [MExtractAsRule] into rule classes.");
        table.AddRow("[yellow]verify[/]", "Verify rules for consistency, circular dependencies, and validity.");
        table.AddRow("[yellow]register[/]", "Generate DI registration + optional generated dispatcher classes.");
        table.AddRow("[yellow]generate-tests[/]", "Generate unit test scaffolds for extracted rules.");
        table.AddRow("[yellow]merge[/]", "Merge runtime JSON, generated *.g.cs rules, or [MExtractAsRule] source folder into a target class.");
        table.AddRow("[yellow]split[/]", "Split rules from a file into individual rule files.");
        table.AddRow("[yellow]watch[/]", "Watch directory for changes and auto-regenerate rules.");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold]Usage Examples:[/]");
        AnsiConsole.MarkupLine("  [dim]muonroi-rule extract --source src/Handlers --output Generated/Rules[/]");
        AnsiConsole.MarkupLine("  [dim]muonroi-rule register --rules Generated/Rules --output Generated/MGeneratedRuleRegistrationExtensions.g.cs[/]");
        AnsiConsole.MarkupLine("  [dim]muonroi-rule register --rules Generated/Rules --output Generated/MGeneratedRuleRegistrationExtensions.g.cs --dispatcher-output Generated/Dispatchers[/]");
        AnsiConsole.MarkupLine("  [dim]muonroi-rule merge --rules-dir Generated/Rules --target src/Handlers/MyHandler.cs --namespace My.App --class MyHandler[/]");
        AnsiConsole.MarkupLine("  [dim]muonroi-rule merge --source-dir src/Handlers/Create --target src/Handlers/Create/FcdCreateMerged.cs --class FcdCreateMerged[/]");
        AnsiConsole.MarkupLine("  [dim]muonroi-rule watch --source src/Handlers --output Generated/Rules[/]");
        AnsiConsole.MarkupLine("\n[dim]Defaults: extract output is inferred to <source>/Rules (or <sourceDir>/Rules), organize-by-namespace=false.[/]");
    }

    public static void WriteSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]+[/] {Markup.Escape(message ?? string.Empty)}");
    }

    public static void WriteError(string message)
    {
        AnsiConsole.MarkupLine($"[red]-[/] {Markup.Escape(message ?? string.Empty)}");
    }

    public static void WriteWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]![/] {Markup.Escape(message ?? string.Empty)}");
    }

    public static void WriteInfo(string message)
    {
        AnsiConsole.MarkupLine($"[blue]>[/] {Markup.Escape(message ?? string.Empty)}");
    }

    public static void WriteLocationError(string message, string file, int line)
    {
        AnsiConsole.MarkupLine($"[red]-[/] [bold]{Markup.Escape(file ?? string.Empty)}:{line}[/] - {Markup.Escape(message ?? string.Empty)}");
    }
}
