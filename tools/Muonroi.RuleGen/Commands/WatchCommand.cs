namespace Muonroi.RuleGen.Commands;

internal static class WatchCommand
{
    public static async Task<int> RunAsync(CommandContext context)
    {
        string? source = OptionReader.GetString(context, "source", context.Config.Extract.SourceDir ?? context.Config.Extract.Source);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("Missing required option --source for watch mode.");
        }

        string watchDir = Path.GetFullPath(source, context.WorkingDirectory);
        if (!Directory.Exists(watchDir))
        {
            throw new DirectoryNotFoundException($"Watch directory not found: {watchDir}");
        }

        AnsiConsole.Write(new Panel(new Markup($"[blue]◉ WATCH MODE[/] — Watching [yellow]{watchDir}[/]\nPress [yellow]Ctrl+C[/] to stop.")).Border(BoxBorder.Double));

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using FileSystemWatcher watcher = new(watchDir, "*.cs");
        watcher.IncludeSubdirectories = true;
        watcher.EnableRaisingEvents = true;

        object gate = new();
        Timer? timer = null;

        void Trigger()
        {
            lock (gate)
            {
                timer?.Dispose();
                timer = new Timer(_ =>
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            int result = await ExtractCommand.RunAsync(context);
                            AnsiConsole.MarkupLine($"[dim]Last extract: {DateTime.Now:HH:mm:ss} — Status: {(result == 0 ? "[green]Success[/]" : "[red]Failed[/]")}[/]");
                        }
                        catch (Exception ex)
                        {
                            CommandLineWriter.WriteError($"Extraction failed: {ex.Message}");
                        }
                    });
                }, null, TimeSpan.FromMilliseconds(600), Timeout.InfiniteTimeSpan);
            }
        }

        watcher.Changed += (_, _) => Trigger();
        watcher.Created += (_, _) => Trigger();
        watcher.Deleted += (_, _) => Trigger();
        watcher.Renamed += (_, _) => Trigger();

        await ExtractCommand.RunAsync(context);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
        }
        catch (TaskCanceledException)
        {
            // graceful shutdown
        }

        lock (gate)
        {
            timer?.Dispose();
        }

        return 0;
    }
}
