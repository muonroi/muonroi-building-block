using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.RuleGen.VisualStudio.Commands;

internal sealed class RuleGenCommands
{
    private sealed class ExtractExecutionPlan
    {
        public IReadOnlyList<string> ExtractTokens { get; set; }
        public IReadOnlyList<string> RegisterTokens { get; set; }
        public string RegistrationFilePath { get; set; }
    }

    private readonly RuleGenVsixPackage _package;
    private readonly DTE2 _dte;
    private readonly ConcurrentDictionary<string, Process> _watchProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, byte> _queryStatusLogged = new();

    private RuleGenCommands(RuleGenVsixPackage package, DTE2 dte)
    {
        _package = package;
        _dte = dte;
    }

    public static async Task InitializeAsync(RuleGenVsixPackage package)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await RuleGenOutput.WriteLineAsync(package, $"Diagnostics file: {RuleGenOutput.GetLogFilePath()}");

        object menuSvc = await package.GetServiceAsync(typeof(IMenuCommandService));
        object dteSvc = await package.GetServiceAsync(typeof(SDTE));
        await RuleGenOutput.WriteLineAsync(package, $"Service probe: IMenuCommandService={(menuSvc is OleMenuCommandService)}, SDTE={(dteSvc is DTE2)}");

        if (menuSvc is not OleMenuCommandService commandService || dteSvc is not DTE2 dte)
        {
            await RuleGenOutput.WriteLineAsync(package, "Failed to initialize commands: IMenuCommandService or DTE is null.");
            return;
        }

        await RuleGenOutput.WriteLineAsync(package, "Registering Muonroi RuleGen commands...");
        RuleGenCommands instance = new(package, dte);
        instance.Register(commandService, PackageIds.ExtractCommandId, instance.ExecuteExtractAsync, instance.CanRunOnAnySelection);
        instance.Register(commandService, PackageIds.MergeCommandId, instance.ExecuteMergeAsync, instance.CanRunMerge);
        instance.Register(commandService, PackageIds.WatchCommandId, instance.ExecuteWatchAsync, instance.CanRunOnAnySelection);
        instance.Register(commandService, PackageIds.StopWatchCommandId, instance.ExecuteStopWatchAsync, instance.CanStopWatch);
        await RuleGenOutput.WriteLineAsync(package, "Muonroi RuleGen commands registered.");
    }

    private void Register(
        OleMenuCommandService commandService,
        int commandId,
        Func<Task> executeAsync,
        Func<bool> canRun)
    {
        CommandID menuCommandId = new(PackageIds.CommandSetGuid, commandId);
        OleMenuCommand menuItem = new(async (_, _) => await executeAsync(), menuCommandId);
        menuItem.BeforeQueryStatus += (_, _) =>
        {
            bool canRunResult = false;
            string selectionInfo = "<unknown>";
            string errorInfo = null;

            try
            {
                canRunResult = canRun();
                selectionInfo = SelectedItemResolver.TryDescribeSelection(_dte, out string description)
                    ? description
                    : "<none>";
            }
            catch (Exception ex)
            {
                errorInfo = ex.GetType().Name + ": " + ex.Message;
            }

            menuItem.Visible = true;
            menuItem.Enabled = canRunResult;

            if (_queryStatusLogged.TryAdd(commandId, 0))
            {
                string msg = $"BeforeQueryStatus cmd=0x{commandId:X4} canRun={canRunResult} selection={selectionInfo}";
                if (!string.IsNullOrWhiteSpace(errorInfo))
                {
                    msg += $" error={errorInfo}";
                }

                ThreadHelper.JoinableTaskFactory.Run(async () => await RuleGenOutput.WriteLineAsync(_package, msg));
            }
        };

        commandService.AddCommand(menuItem);
        ThreadHelper.JoinableTaskFactory.Run(async () =>
            await RuleGenOutput.WriteLineAsync(_package, $"Registered command 0x{commandId:X4} ({GetCommandName(commandId)})"));
    }

    private bool CanRunOnAnySelection()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return SelectedItemResolver.TryGetSelection(_dte, out _);
    }

    private bool CanRunMerge()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        return SelectedItemResolver.TryGetSelection(_dte, out _);
    }

    private bool CanStopWatch()
    {
        return !_watchProcesses.IsEmpty;
    }

    private async Task ExecuteExtractAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await RuleGenOutput.WriteLineAsync(_package, "ExecuteOneShot invoked for 'extract'.");

        if (!SelectedItemResolver.TryGetSelection(_dte, out SelectionContext selection) || selection == null)
        {
            await RuleGenOutput.WriteLineAsync(_package, "extract: no valid selection.");
            ShowError("Please select exactly one file/folder/project in Solution Explorer.");
            return;
        }

        await RuleGenOutput.WriteLineAsync(_package, $"extract: selection {selection.Kind} | {selection.Path}");

        ExtractExecutionPlan plan;
        try
        {
            plan = await BuildExtractExecutionPlanAsync(selection);
            if (plan is null || plan.ExtractTokens.Count == 0)
            {
                await RuleGenOutput.WriteLineAsync(_package, "extract: cancelled by user.");
                return;
            }

            await RuleGenOutput.WriteLineAsync(_package, $"extract: tokens = {string.Join(" ", plan.ExtractTokens)}");
            if (plan.RegisterTokens is not null && plan.RegisterTokens.Count > 0)
            {
                await RuleGenOutput.WriteLineAsync(_package, $"register: tokens = {string.Join(" ", plan.RegisterTokens)}");
            }
        }
        catch (Exception ex)
        {
            await RuleGenOutput.WriteLineAsync(_package, $"extract: plan build failed: {ex.GetType().Name}: {ex.Message}");
            ShowError(ex.Message);
            return;
        }

        int extractExitCode = await RunCommandOnceAsync(selection, plan.ExtractTokens, "extract");
        if (extractExitCode != 0)
        {
            ShowError($"extract failed with exit code {extractExitCode}. Check Output > Muonroi RuleGen.");
            return;
        }

        if (plan.RegisterTokens is not null && plan.RegisterTokens.Count > 0)
        {
            int registerExitCode = await RunCommandOnceAsync(selection, plan.RegisterTokens, "register");
            if (registerExitCode != 0)
            {
                ShowError($"register failed with exit code {registerExitCode}. Check Output > Muonroi RuleGen.");
                return;
            }

            ShowInfo($"Extract + register succeeded.\n\nRegistration: {plan.RegistrationFilePath}");
            return;
        }

        ShowInfo("Extract completed successfully.");
    }

    private async Task ExecuteMergeAsync()
    {
        await ExecuteOneShotAsync(BuildMergeTokensAsync, "merge");
    }

    private async Task ExecuteWatchAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await RuleGenOutput.WriteLineAsync(_package, "ExecuteWatch invoked.");

        if (!SelectedItemResolver.TryGetSelection(_dte, out SelectionContext selection) || selection == null)
        {
            await RuleGenOutput.WriteLineAsync(_package, "ExecuteWatch: no valid selection.");
            ShowError("Please select exactly one file/folder/project in Solution Explorer.");
            return;
        }

        await RuleGenOutput.WriteLineAsync(_package, $"ExecuteWatch selection: {selection.Kind} | {selection.Path}");

        IReadOnlyList<string> tokens = await BuildWatchTokensAsync(selection);
        if (tokens is null || tokens.Count == 0)
        {
            await RuleGenOutput.WriteLineAsync(_package, "watch cancelled by user.");
            return;
        }

        string watchKey = selection.WorkingDirectory;
        if (_watchProcesses.ContainsKey(watchKey))
        {
            await RuleGenOutput.WriteLineAsync(_package, $"Watch already running for '{watchKey}'.");
            return;
        }

        RuleGenOptionsPage options = _package.GetOptionsPage();
        RuleGenProcessSpec spec = RuleGenProcessFactory.Create(selection, options, tokens);

        await RuleGenOutput.WriteLineAsync(_package, $"> {spec.FileName} {spec.Arguments}");
        Process process = RuleGenProcessRunner.StartBackground(spec, line => RuleGenOutput.WriteLineAsync(_package, line));

        _watchProcesses[watchKey] = process;
        process.Exited += async (_, _) =>
        {
            _watchProcesses.TryRemove(watchKey, out _);
            await RuleGenOutput.WriteLineAsync(_package, $"Watch stopped for '{watchKey}' (exit: {process.ExitCode}).");
            process.Dispose();
        };

        await RuleGenOutput.WriteLineAsync(_package, $"Watch started for '{watchKey}' (PID {process.Id}).");
    }

    private async Task ExecuteStopWatchAsync()
    {
        await RuleGenOutput.WriteLineAsync(_package, "ExecuteStopWatch invoked.");

        if (_watchProcesses.IsEmpty)
        {
            await RuleGenOutput.WriteLineAsync(_package, "No watch process is running.");
            return;
        }

        if (!RuleGenPromptService.ConfirmYesNo(
                _package,
                "Muonroi RuleGen - Stop Watch",
                "Stop all active watch processes?",
                defaultYes: true))
        {
            await RuleGenOutput.WriteLineAsync(_package, "stop-watch cancelled by user.");
            return;
        }

        foreach (KeyValuePair<string, Process> pair in _watchProcesses)
        {
            try
            {
                if (!pair.Value.HasExited)
                {
                    pair.Value.Kill();
                }
            }
            catch (Exception ex)
            {
                await RuleGenOutput.WriteLineAsync(_package, $"Failed to stop watch '{pair.Key}': {ex.Message}");
            }
        }

        _watchProcesses.Clear();
        await RuleGenOutput.WriteLineAsync(_package, "All watch processes stopped.");
    }

    private async Task ExecuteOneShotAsync(Func<SelectionContext, Task<IReadOnlyList<string>>> tokenBuilder, string commandName)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await RuleGenOutput.WriteLineAsync(_package, $"ExecuteOneShot invoked for '{commandName}'.");

        if (!SelectedItemResolver.TryGetSelection(_dte, out SelectionContext selection) || selection == null)
        {
            await RuleGenOutput.WriteLineAsync(_package, $"{commandName}: no valid selection.");
            ShowError("Please select exactly one file/folder/project in Solution Explorer.");
            return;
        }

        await RuleGenOutput.WriteLineAsync(_package, $"{commandName}: selection {selection.Kind} | {selection.Path}");

        IReadOnlyList<string> tokens;
        try
        {
            tokens = await tokenBuilder(selection);
            if (tokens is null || tokens.Count == 0)
            {
                await RuleGenOutput.WriteLineAsync(_package, $"{commandName}: cancelled by user.");
                return;
            }

            await RuleGenOutput.WriteLineAsync(_package, $"{commandName}: tokens = {string.Join(" ", tokens)}");
        }
        catch (Exception ex)
        {
            await RuleGenOutput.WriteLineAsync(_package, $"{commandName}: token build failed: {ex.GetType().Name}: {ex.Message}");
            ShowError(ex.Message);
            return;
        }

        int exitCode = await RunCommandOnceAsync(selection, tokens, commandName);
        if (exitCode == 0)
        {
            ShowInfo($"{commandName} completed successfully.");
            return;
        }

        ShowError($"{commandName} failed with exit code {exitCode}. Check Output > Muonroi RuleGen.");
    }

    private async Task<int> RunCommandOnceAsync(SelectionContext selection, IReadOnlyList<string> tokens, string commandName)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        IDisposable progressDialog = RuleGenPromptService.ShowProgressDialog(
            $"Muonroi RuleGen - {commandName}",
            $"Running '{commandName}'...");

        try
        {
            RuleGenOptionsPage options = _package.GetOptionsPage();
            RuleGenProcessSpec spec = RuleGenProcessFactory.Create(selection, options, tokens);

            await RuleGenOutput.WriteLineAsync(_package, $"> {spec.FileName} {spec.Arguments}");
            using CancellationTokenSource cts = new();
            int exitCode = await RuleGenProcessRunner.RunOnceAsync(spec, line => RuleGenOutput.WriteLineAsync(_package, line), cts.Token);

            if (exitCode == 0)
            {
                await RuleGenOutput.WriteLineAsync(_package, $"{commandName} completed successfully.");
            }
            else
            {
                await RuleGenOutput.WriteLineAsync(_package, $"{commandName} failed with exit code {exitCode}.");
            }

            return exitCode;
        }
        finally
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            progressDialog.Dispose();
        }
    }

    private async Task<ExtractExecutionPlan> BuildExtractExecutionPlanAsync(SelectionContext selection)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (!RuleGenPromptService.TryPromptExtractDialog(selection, out ExtractDialogResult input))
        {
            return null;
        }

        string folderName = string.IsNullOrWhiteSpace(input.RuleFolderName) ? "Rules" : input.RuleFolderName.Trim();
        string outputPath = string.IsNullOrWhiteSpace(input.OutputPath)
            ? RuleGenPromptService.ComputeDefaultExtractOutput(selection, folderName)
            : input.OutputPath.Trim();
        string defaultNamespace = RuleGenPromptService.ComputeDefaultExtractNamespace(selection, folderName);
        bool namespaceLooksAuto = string.IsNullOrWhiteSpace(input.Namespace) ||
            string.Equals(input.Namespace.Trim(), defaultNamespace, StringComparison.Ordinal);
        string outputBasedNamespace = RuleGenPromptService.ComputeNamespaceFromOutputPath(
            selection,
            outputPath,
            outputIsFilePath: false);
        string effectiveNamespace = namespaceLooksAuto
            ? (string.IsNullOrWhiteSpace(outputBasedNamespace) ? defaultNamespace : outputBasedNamespace)
            : input.Namespace.Trim();

        if (Directory.Exists(outputPath) &&
            Directory.EnumerateFiles(outputPath, "*.g.cs", SearchOption.AllDirectories).Any() &&
            !RuleGenPromptService.ConfirmYesNo(
                _package,
                "Muonroi RuleGen - Extract",
                $"Output folder already contains generated rules.\n\n{outputPath}\n\nOverwrite existing generated files?",
                defaultYes: false))
        {
            return null;
        }

        List<string> extractTokens = ["extract", input.SourceOption, input.SourceValue, "--output", outputPath];
        AddOption(extractTokens, "--namespace", effectiveNamespace);
        AddOption(extractTokens, "--context", input.Context);
        AddOption(extractTokens, "--pattern", input.Pattern);
        AddOption(extractTokens, "--exclude", input.Exclude);
        AddOption(extractTokens, "--tenant", input.Tenant);
        AddBool(extractTokens, "--generate-tests", input.GenerateTests);
        AddBool(extractTokens, "--validate", input.Validate);
        AddBool(extractTokens, "--organize-by-namespace", input.OrganizeByNamespace);
        AddBool(extractTokens, "--parallel", input.Parallel);
        extractTokens.AddRange(RuleGenPromptService.SplitArguments(input.Raw));

        List<string> registerTokens = null;
        string registrationFilePath = null;
        if (input.AutoRegister)
        {
            string registrationFileName = string.IsNullOrWhiteSpace(input.RegistrationFileName)
                ? "MGeneratedRuleRegistrationExtensions.g.cs"
                : input.RegistrationFileName.Trim();
            if (!registrationFileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                registrationFileName += ".g.cs";
            }

            registrationFilePath = Path.IsPathRooted(registrationFileName)
                ? registrationFileName
                : Path.Combine(outputPath, registrationFileName);
            string registrationNamespace = string.IsNullOrWhiteSpace(input.RegistrationNamespace)
                ? effectiveNamespace
                : input.RegistrationNamespace.Trim();

            registerTokens = ["register", "--rules", outputPath, "--output", registrationFilePath];
            AddOption(registerTokens, "--namespace", registrationNamespace);
            AddOption(registerTokens, "--registration-class", input.RegistrationClassName);
            AddBool(registerTokens, "--generate-dispatchers", input.GenerateDispatchers);
            AddBool(registerTokens, "--register-dispatchers", input.RegisterDispatchers);
            AddBool(registerTokens, "--include-rule-engine", input.IncludeRuleEngine);
            AddOption(registerTokens, "--dispatcher-output", input.DispatcherOutput);
            AddOption(registerTokens, "--dispatcher-namespace", input.DispatcherNamespace);
            AddBool(registerTokens, "--dispatcher-overwrite", input.DispatcherOverwrite);
        }

        return new ExtractExecutionPlan
        {
            ExtractTokens = extractTokens,
            RegisterTokens = registerTokens,
            RegistrationFilePath = registrationFilePath
        };
    }

    private async Task<IReadOnlyList<string>> BuildMergeTokensAsync(SelectionContext selection)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (!RuleGenPromptService.TryPromptMergeDialog(selection, out MergeDialogResult input))
        {
            return null;
        }

        if (File.Exists(input.TargetPath) &&
            !RuleGenPromptService.ConfirmYesNo(
                _package,
                "Muonroi RuleGen - Merge",
                $"Target file already exists.\n\n{input.TargetPath}\n\nOverwrite?",
                defaultYes: false))
        {
            return null;
        }

        List<string> tokens = ["merge"];
        if (input.Mode == "json")
        {
            AddOption(tokens, "--rules-json", input.SourcePath);
        }
        else if (input.Mode == "attribute")
        {
            AddOption(tokens, "--source-dir", input.SourcePath);
        }
        else
        {
            AddOption(tokens, "--rules-dir", input.SourcePath);
        }

        AddOption(tokens, "--target", input.TargetPath);
        AddOption(tokens, "--class", input.ClassName);
        string effectiveNamespace = string.IsNullOrWhiteSpace(input.Namespace)
            ? RuleGenPromptService.ComputeNamespaceFromOutputPath(selection, input.TargetPath, outputIsFilePath: true)
            : input.Namespace;
        AddOption(tokens, "--namespace", effectiveNamespace);
        AddOption(tokens, "--context", input.Context);
        AddOption(tokens, "--pattern", input.Pattern);
        AddOption(tokens, "--exclude", input.Exclude);
        AddBool(tokens, "--recursive", input.Recursive);
        AddBool(tokens, "--parallel", input.Parallel);
        AddBool(tokens, "--compile-check", input.CompileCheck);
        AddOption(tokens, "--compile-target", input.CompileTarget);
        AddOption(tokens, "--strategy", input.Strategy);
        AddOption(tokens, "--workflow", input.Workflow);
        AddOption(tokens, "--tenant", input.Tenant);
        tokens.AddRange(RuleGenPromptService.SplitArguments(input.Raw));
        return tokens;
    }

    private async Task<IReadOnlyList<string>> BuildWatchTokensAsync(SelectionContext selection)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (!RuleGenPromptService.TryPromptWatchDialog(selection, out WatchDialogResult input))
        {
            return null;
        }

        if (input.ConfigureOutput &&
            Directory.Exists(input.OutputPath) &&
            Directory.EnumerateFiles(input.OutputPath, "*.g.cs", SearchOption.AllDirectories).Any() &&
            !RuleGenPromptService.ConfirmYesNo(
                _package,
                "Muonroi RuleGen - Watch",
                $"Output folder already contains generated rules.\n\n{input.OutputPath}\n\nContinue and allow overwrite?",
                defaultYes: false))
        {
            return null;
        }

        List<string> tokens = ["watch", "--source", input.SourcePath];
        if (input.ConfigureOutput)
        {
            AddOption(tokens, "--output", input.OutputPath);
        }

        AddOption(tokens, "--namespace", input.Namespace);
        AddOption(tokens, "--context", input.Context);
        AddOption(tokens, "--pattern", input.Pattern);
        AddOption(tokens, "--exclude", input.Exclude);
        AddOption(tokens, "--tenant", input.Tenant);
        AddBool(tokens, "--generate-tests", input.GenerateTests);
        AddBool(tokens, "--validate", input.Validate);
        AddBool(tokens, "--organize-by-namespace", input.OrganizeByNamespace);
        AddBool(tokens, "--parallel", input.Parallel);
        tokens.AddRange(RuleGenPromptService.SplitArguments(input.Raw));
        return tokens;
    }

    private void ShowError(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        VsShellUtilities.ShowMessageBox(
            _package,
            message,
            "Muonroi RuleGen",
            OLEMSGICON.OLEMSGICON_WARNING,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }

    private void ShowInfo(string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        VsShellUtilities.ShowMessageBox(
            _package,
            message,
            "Muonroi RuleGen",
            OLEMSGICON.OLEMSGICON_INFO,
            OLEMSGBUTTON.OLEMSGBUTTON_OK,
            OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
    }

    private static string GetCommandName(int commandId)
    {
        return commandId switch
        {
            PackageIds.ExtractCommandId => "Extract",
            PackageIds.MergeCommandId => "Merge",
            PackageIds.WatchCommandId => "Watch",
            PackageIds.StopWatchCommandId => "StopWatch",
            _ => $"Unknown({commandId})"
        };
    }

    private static void AddOption(List<string> tokens, string optionName, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        tokens.Add(optionName);
        tokens.Add(value.Trim());
    }

    private static void AddBool(List<string> tokens, string optionName, bool value)
    {
        tokens.Add(optionName);
        tokens.Add(value ? "true" : "false");
    }
}
