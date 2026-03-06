using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Muonroi.RuleGen.VisualStudio;

internal static class RuleGenOutput
{
    private const string PaneTitle = "Muonroi RuleGen";
    private static IVsOutputWindowPane s_pane;
    private static readonly string s_logFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Muonroi",
        "RuleGenVSIX",
        "rulegen-vsix.log");

    public static async Task WriteLineAsync(AsyncPackage package, string message)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        IVsOutputWindowPane pane = await GetPaneAsync(package);
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        pane.OutputStringThreadSafe($"{line}{Environment.NewLine}");
        pane.Activate();
        AppendFileLog(line);
    }

    public static string GetLogFilePath()
    {
        return s_logFilePath;
    }

    private static async Task<IVsOutputWindowPane> GetPaneAsync(AsyncPackage package)
    {
        if (s_pane is not null)
        {
            return s_pane;
        }

        IVsOutputWindow output = (IVsOutputWindow)await package.GetServiceAsync(typeof(SVsOutputWindow));
        Guid paneGuid = PackageIds.OutputPaneGuid;

        output.CreatePane(ref paneGuid, PaneTitle, fInitVisible: 1, fClearWithSolution: 1);
        int hr = output.GetPane(ref paneGuid, out IVsOutputWindowPane pane);
        ErrorHandler.ThrowOnFailure(hr);

        s_pane = pane;
        return pane;
    }

    private static void AppendFileLog(string line)
    {
        try
        {
            string dir = Path.GetDirectoryName(s_logFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllText(s_logFilePath, $"{line}{Environment.NewLine}");
        }
        catch
        {
            // ignore diagnostics file write failures
        }
    }
}
