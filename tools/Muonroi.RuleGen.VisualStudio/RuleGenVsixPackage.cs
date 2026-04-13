using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.RuleGen.VisualStudio;

/// <summary>
/// VSIX package entry point for Muonroi RuleGen.
/// </summary>
[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration("Muonroi RuleGen", "Extract/Merge/Watch commands for Muonroi.RuleGen", "1.0")]
[ProvideMenuResource("Menus.ctmenu", 1)]
[ProvideOptionPage(typeof(RuleGenOptionsPage), "Muonroi", "RuleGen", 0, 0, true)]
[ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82", PackageAutoLoadFlags.BackgroundLoad)]
[Guid(PackageIds.PackageGuidString)]
public sealed class RuleGenVsixPackage : AsyncPackage
{
    internal RuleGenOptionsPage GetOptionsPage()
    {
        return (RuleGenOptionsPage)GetDialogPage(typeof(RuleGenOptionsPage));
    }

    /// <summary>
    /// Initializes the package and registers commands.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="progress">Progress reporter.</param>
    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

        if (await GetServiceAsync(typeof(SVsActivityLog)) is IVsActivityLog activityLog)
        {
            activityLog.LogEntry((uint)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, nameof(RuleGenVsixPackage), "Muonroi.RuleGen VSIX package initializing.");
        }

        await RuleGenOutput.WriteLineAsync(this, "Muonroi.RuleGen VSIX package initializing...");
        await Commands.RuleGenCommands.InitializeAsync(this);
        await RuleGenOutput.WriteLineAsync(this, "Muonroi.RuleGen VSIX package initialized.");

        if (await GetServiceAsync(typeof(SVsActivityLog)) is IVsActivityLog activityLogAfterInit)
        {
            activityLogAfterInit.LogEntry((uint)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION, nameof(RuleGenVsixPackage), "Muonroi.RuleGen VSIX package initialized.");
        }
    }
}
