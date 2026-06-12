using System.Diagnostics;

namespace Muonroi.RuleGen.Tests;

internal sealed record CliRunResult(int ExitCode, string StdOut, string StdErr)
{
    public string Combined => string.Join(Environment.NewLine, new[] { StdOut, StdErr }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

internal static class CliProcess
{
    private static readonly SemaphoreSlim BuildGate = new(1, 1);
    private static bool _built;

    public static async Task<CliRunResult> RunAsync(params string[] args)
    {
        string repoRoot = GetRepoRoot();
        string toolDll = await EnsureToolBuiltAsync(repoRoot);

        ProcessStartInfo psi = new("dotnet")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add(toolDll);
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start Muonroi.RuleGen process.");
        string stdout = await process.StandardOutput.ReadToEndAsync();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CliRunResult(process.ExitCode, stdout, stderr);
    }

    public static string GetRepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string solution = Path.Combine(directory.FullName, "Muonroi.BuildingBlock.sln");
            if (File.Exists(solution))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate repository root from test base directory.");
    }

    private static async Task<string> EnsureToolBuiltAsync(string repoRoot)
    {
        await BuildGate.WaitAsync();
        try
        {
            string toolDll = Path.Combine(repoRoot, "tools", "Muonroi.RuleGen", "bin", "Debug", "net8.0", "Muonroi.RuleGen.dll");
            if (_built && File.Exists(toolDll))
            {
                return toolDll;
            }

            string csproj = Path.Combine(repoRoot, "tools", "Muonroi.RuleGen", "Muonroi.RuleGen.csproj");
            ProcessStartInfo buildInfo = new("dotnet")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            buildInfo.ArgumentList.Add("build");
            buildInfo.ArgumentList.Add(csproj);
            buildInfo.ArgumentList.Add("-c");
            buildInfo.ArgumentList.Add("Debug");
            buildInfo.ArgumentList.Add("-nologo");

            // Disable MSBuild node reuse / build server so this one-shot build does not leave
            // long-lived "dotnet" worker nodes behind. Orphaned nodes keep the test host's
            // process tree alive after tests complete, causing `dotnet test` to hang until the
            // node-reuse timeout (~15 min) elapses.
            buildInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
            buildInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

            using Process process = Process.Start(buildInfo) ?? throw new InvalidOperationException("Cannot build Muonroi.RuleGen for tests.");
            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"RuleGen build failed.\n{stdout}\n{stderr}");
            }

            if (!File.Exists(toolDll))
            {
                throw new FileNotFoundException($"Built RuleGen DLL not found: {toolDll}");
            }

            _built = true;
            return toolDll;
        }
        finally
        {
            BuildGate.Release();
        }
    }
}
