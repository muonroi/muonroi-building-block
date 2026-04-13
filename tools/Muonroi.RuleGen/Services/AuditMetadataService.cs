using System.Diagnostics;

namespace Muonroi.RuleGen.Services;

internal static class AuditMetadataService
{
    public static string ResolveAuthor()
    {
        string? env = Environment.GetEnvironmentVariable("GIT_AUTHOR_EMAIL");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        string? gitValue = TryRunGit("config user.email");
        if (!string.IsNullOrWhiteSpace(gitValue))
        {
            return gitValue;
        }

        return Environment.UserName;
    }

    public static string? ResolveGitCommit(string workingDirectory)
    {
        return TryRunGit("rev-parse --short HEAD", workingDirectory);
    }

    private static string? TryRunGit(string arguments, string? workingDirectory = null)
    {
        try
        {
            ProcessStartInfo psi = new("git", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
            };

            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            process.WaitForExit(1500);
            if (process.ExitCode != 0)
            {
                return null;
            }

            string text = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
