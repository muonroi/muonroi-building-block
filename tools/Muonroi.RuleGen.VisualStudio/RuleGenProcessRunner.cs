using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.RuleGen.VisualStudio;

internal sealed class RuleGenProcessSpec
{
    public string FileName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
}

internal static class RuleGenProcessFactory
{
    public static RuleGenProcessSpec Create(SelectionContext selection, RuleGenOptionsPage options, IReadOnlyList<string> commandTokens)
    {
        string extraArgs = (options.DefaultExtraArgs ?? string.Empty).Trim();
        string commandArgs = BuildTokenArguments(commandTokens, extraArgs);

        string configured = FirstNonEmpty(options.ExecutablePath, Environment.GetEnvironmentVariable("MUONROI_RULEGEN_EXE"));
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string configuredPath = configured.Trim();
            if (configuredPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return new RuleGenProcessSpec
                {
                    FileName = "dotnet",
                    Arguments = $"\"{configuredPath}\" {commandArgs}",
                    WorkingDirectory = selection.WorkingDirectory
                };
            }

            return new RuleGenProcessSpec
            {
                FileName = configuredPath,
                Arguments = commandArgs,
                WorkingDirectory = selection.WorkingDirectory
            };
        }

        if (TryFindCommandOnPath("muonroi-rule", out string packagedCommandPath))
        {
            return new RuleGenProcessSpec
            {
                FileName = packagedCommandPath,
                Arguments = commandArgs,
                WorkingDirectory = selection.WorkingDirectory
            };
        }

        if (HasLocalToolManifestWithMuonroiRule(selection.WorkingDirectory))
        {
            return new RuleGenProcessSpec
            {
                FileName = "dotnet",
                Arguments = $"tool run muonroi-rule -- {commandArgs}",
                WorkingDirectory = selection.WorkingDirectory
            };
        }

        if (options.EnableSourceProjectFallback &&
            TryResolveSourceProjectPath(selection.WorkingDirectory, options, out string sourceProjectPath))
        {
            return new RuleGenProcessSpec
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{sourceProjectPath}\" -- {commandArgs}",
                WorkingDirectory = selection.WorkingDirectory
            };
        }

        return new RuleGenProcessSpec
        {
            FileName = "dotnet",
            Arguments = $"tool run muonroi-rule -- {commandArgs}",
            WorkingDirectory = selection.WorkingDirectory
        };
    }

    private static string BuildTokenArguments(IReadOnlyList<string> tokens, string extraArgs)
    {
        StringBuilder builder = new StringBuilder();
        foreach (string token in tokens)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(Quote(token));
        }

        if (!string.IsNullOrWhiteSpace(extraArgs))
        {
            builder.Append(' ');
            builder.Append(extraArgs);
        }

        return builder.ToString();
    }

    private static string Quote(string value)
    {
        if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }

    private static string FindRepoRoot(string fromDirectory)
    {
        DirectoryInfo dir = new DirectoryInfo(fromDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Muonroi.BuildingBlock.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }

    private static bool TryResolveSourceProjectPath(string workingDirectory, RuleGenOptionsPage options, out string sourceProjectPath)
    {
        sourceProjectPath = string.Empty;

        string configuredProjectPath = FirstNonEmpty(
            options.SourceProjectPath,
            Environment.GetEnvironmentVariable("MUONROI_RULEGEN_PROJECT"));

        if (!string.IsNullOrWhiteSpace(configuredProjectPath) && File.Exists(configuredProjectPath))
        {
            sourceProjectPath = configuredProjectPath;
            return true;
        }

        string repoRoot = FindRepoRoot(workingDirectory);
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            string csprojInRepo = Path.Combine(repoRoot, "tools", "Muonroi.RuleGen", "Muonroi.RuleGen.csproj");
            if (File.Exists(csprojInRepo))
            {
                sourceProjectPath = csprojInRepo;
                return true;
            }
        }

        DirectoryInfo dir = new DirectoryInfo(workingDirectory);
        while (dir is not null)
        {
            string candidate1 = Path.Combine(dir.FullName, "MuonroiBuildingBlock", "tools", "Muonroi.RuleGen", "Muonroi.RuleGen.csproj");
            if (File.Exists(candidate1))
            {
                sourceProjectPath = candidate1;
                return true;
            }

            string candidate2 = Path.Combine(dir.FullName, "Core", "MuonroiBuildingBlock", "tools", "Muonroi.RuleGen", "Muonroi.RuleGen.csproj");
            if (File.Exists(candidate2))
            {
                sourceProjectPath = candidate2;
                return true;
            }

            dir = dir.Parent;
        }

        return false;
    }

    private static bool TryFindCommandOnPath(string commandName, out string fullPath)
    {
        fullPath = string.Empty;

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        string[] pathParts = pathValue.Split(Path.PathSeparator);
        string[] extensions = GetExecutableExtensions();

        foreach (string rawDir in pathParts)
        {
            string dir = (rawDir ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                continue;
            }

            string direct = Path.Combine(dir, commandName);
            if (File.Exists(direct))
            {
                fullPath = direct;
                return true;
            }

            foreach (string ext in extensions)
            {
                string candidate = Path.Combine(dir, commandName + ext);
                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static string[] GetExecutableExtensions()
    {
        string pathext = Environment.GetEnvironmentVariable("PATHEXT") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pathext))
        {
            return new[] { ".exe", ".cmd", ".bat", ".com" };
        }

        return [.. pathext
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.StartsWith(".") ? s : "." + s)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static bool HasLocalToolManifestWithMuonroiRule(string fromDirectory)
    {
        DirectoryInfo dir = new DirectoryInfo(fromDirectory);
        while (dir is not null)
        {
            string manifestPath = Path.Combine(dir.FullName, ".config", "dotnet-tools.json");
            if (File.Exists(manifestPath))
            {
                string content = File.ReadAllText(manifestPath);
                if (content.IndexOf("\"muonroi-rule\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    content.IndexOf("\"muonroi.rulegen\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            dir = dir.Parent;
        }

        return false;
    }
}

internal static class RuleGenProcessRunner
{
    public static async Task<int> RunOnceAsync(RuleGenProcessSpec spec, Func<string, Task> log, CancellationToken cancellationToken)
    {
        using Process process = CreateProcess(spec);

        process.OutputDataReceived += async (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                await log(e.Data);
            }
        };

        process.ErrorDataReceived += async (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                await log($"ERROR: {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using CancellationTokenRegistration _ = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // ignore
            }
        });

        await WaitForExitAsync(process, cancellationToken);
        return process.ExitCode;
    }

    public static Process StartBackground(RuleGenProcessSpec spec, Func<string, Task> log)
    {
        Process process = CreateProcess(spec);
        process.OutputDataReceived += async (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                await log(e.Data);
            }
        };

        process.ErrorDataReceived += async (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                await log($"ERROR: {e.Data}");
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static Process CreateProcess(RuleGenProcessSpec spec)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory = spec.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        return new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
    }

    private static Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return Task.CompletedTask;
        }

        TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
        EventHandler handler = null;
        handler = (_, _) =>
        {
            process.Exited -= handler;
            tcs.TrySetResult(null);
        };

        process.Exited += handler;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                process.Exited -= handler;
                tcs.TrySetCanceled();
            });
        }

        return tcs.Task;
    }
}
