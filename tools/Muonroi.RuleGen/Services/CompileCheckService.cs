using System.Diagnostics;

namespace Muonroi.RuleGen.Services;

internal sealed record CompileCheckResult(bool Success, int ExitCode, string Output, string TargetPath);

internal static class CompileCheckService
{
    public static string? DiscoverNearestCompileTarget(string startDirectory)
    {
        DirectoryInfo? dir = new(startDirectory);
        while (dir is not null)
        {
            FileInfo? sln = dir.GetFiles("*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (sln is not null)
            {
                return sln.FullName;
            }

            FileInfo? csproj = dir.GetFiles("*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj is not null)
            {
                return csproj.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    public static async Task<CompileCheckResult> BuildAsync(
        string compileTargetPath,
        CancellationToken cancellationToken = default)
    {
        string fullTarget = Path.GetFullPath(compileTargetPath);
        if (!File.Exists(fullTarget))
        {
            throw new FileNotFoundException($"Compile target not found: {fullTarget}");
        }

        string workingDirectory = Path.GetDirectoryName(fullTarget)!;
        ProcessStartInfo psi = new("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("build");
        psi.ArgumentList.Add(fullTarget);
        psi.ArgumentList.Add("-nologo");
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("minimal");

        // This is a one-shot compile-check: the tool exits immediately after the build, so
        // MSBuild node reuse / the persistent build server provide no benefit and instead leak
        // long-lived child "dotnet" worker nodes (~15 min lifetime) that outlive this process.
        // Disable both so the spawned build leaves no orphaned process behind.
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        psi.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        using Process process = Process.Start(psi)
            ?? throw new InvalidOperationException("Unable to start dotnet build process for compile-check.");

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        string stdout = await stdoutTask;
        string stderr = await stderrTask;
        string combined = string.Join(
            Environment.NewLine,
            new[] { stdout, stderr }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new CompileCheckResult(process.ExitCode == 0, process.ExitCode, combined, fullTarget);
    }
}
