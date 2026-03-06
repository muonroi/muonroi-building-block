using Muonroi.RuleGen.Cli;
using Muonroi.RuleGen.Commands;

namespace Muonroi.RuleGen;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            CommandLineWriter.PrintUsage();
            return 1;
        }

        string command = args[0].Trim().ToLowerInvariant();
        IReadOnlyDictionary<string, string> raw = CommandLineParser.Parse([.. args.Skip(1)]);
        CommandContext context = CommandContext.Create(raw);

        try
        {
            return command switch
            {
                "extract" => await ExtractCommand.RunAsync(context),
                "verify" => await VerifyCommand.RunAsync(context),
                "register" => await RegisterCommand.RunAsync(context),
                "generate-tests" => await GenerateTestsCommand.RunAsync(context),
                "merge" => await MergeCommand.RunAsync(context),
                "split" => await SplitCommand.RunAsync(context),
                "watch" => await WatchCommand.RunAsync(context),
                "help" or "--help" or "-h" => PrintHelp(),
                _ => Fail($"Unknown command '{command}'.")
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[rulegen] ERROR: {ex.Message}");
            return 1;
        }
    }

    private static int PrintHelp()
    {
        CommandLineWriter.PrintUsage();
        return 0;
    }

    internal static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        CommandLineWriter.PrintUsage();
        return 1;
    }
}
