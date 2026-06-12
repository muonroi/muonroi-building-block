using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Muonroi.RuleGen.VisualStudio;

internal sealed class ExtractDialogResult
{
    public string SourceOption { get; set; } = string.Empty;
    public string SourceValue { get; set; } = string.Empty;
    public string RuleFolderName { get; set; } = "Rules";
    public string OutputPath { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Pattern { get; set; } = "**/*.cs";
    public string Exclude { get; set; } = string.Empty;
    public string Tenant { get; set; } = string.Empty;
    public bool GenerateTests { get; set; }
    public bool Validate { get; set; }
    public bool OrganizeByNamespace { get; set; }
    public bool Parallel { get; set; } = true;
    public bool AutoRegister { get; set; } = true;
    public string RegistrationFileName { get; set; } = "MGeneratedRuleRegistrationExtensions.g.cs";
    public string RegistrationClassName { get; set; } = "MGeneratedRuleRegistrationExtensions";
    public string RegistrationNamespace { get; set; } = string.Empty;
    public bool GenerateDispatchers { get; set; } = true;
    public bool RegisterDispatchers { get; set; } = true;
    public bool IncludeRuleEngine { get; set; } = true;
    public string DispatcherOutput { get; set; } = string.Empty;
    public string DispatcherNamespace { get; set; } = string.Empty;
    public bool DispatcherOverwrite { get; set; } = true;
    public string Raw { get; set; } = string.Empty;
}

internal sealed class MergeDialogResult
{
    public string Mode { get; set; } = "generated";
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Pattern { get; set; } = "*.g.cs";
    public string Exclude { get; set; } = string.Empty;
    public bool Recursive { get; set; } = true;
    public bool Parallel { get; set; } = true;
    public bool CompileCheck { get; set; } = true;
    public string CompileTarget { get; set; } = string.Empty;
    public string Strategy { get; set; } = "append";
    public string Workflow { get; set; } = string.Empty;
    public string Tenant { get; set; } = string.Empty;
    public string Raw { get; set; } = string.Empty;
}

internal sealed class WatchDialogResult
{
    public string SourcePath { get; set; } = string.Empty;
    public bool ConfigureOutput { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public string Pattern { get; set; } = "**/*.cs";
    public string Exclude { get; set; } = string.Empty;
    public string Tenant { get; set; } = string.Empty;
    public bool GenerateTests { get; set; }
    public bool Validate { get; set; }
    public bool OrganizeByNamespace { get; set; }
    public bool Parallel { get; set; } = true;
    public string Raw { get; set; } = string.Empty;
}

internal static class RuleGenPromptService
{
    private static readonly Regex FileScopedNamespaceRegex =
        new(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex BlockNamespaceRegex =
        new(@"^\s*namespace\s+([A-Za-z_][A-Za-z0-9_\.]*)\s*\{", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static bool ConfirmYesNo(IServiceProvider serviceProvider, string title, string message, bool defaultYes)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        int result = VsShellUtilities.ShowMessageBox(
            serviceProvider,
            message,
            title,
            OLEMSGICON.OLEMSGICON_QUERY,
            OLEMSGBUTTON.OLEMSGBUTTON_YESNO,
            defaultYes ? OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST : OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_SECOND);

        return result == (int)VSConstants.MessageBoxResult.IDYES;
    }

    public static IDisposable ShowProgressDialog(string title, string message)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        ProcessingDialogForm form = new(title, message);
        form.Show();
        form.BringToFront();
        return new ProcessingDialogScope(form);
    }

    public static bool TryPromptExtractDialog(SelectionContext selection, out ExtractDialogResult result)
    {
        string sourceOption = selection.Kind switch
        {
            SelectionKind.Project => "--project",
            SelectionKind.File => "--source",
            SelectionKind.Folder => "--source-dir",
            _ => "--source"
        };
        string sourceValue = selection.Path;

        string folderName = "Rules";
        string outputPath = ComputeDefaultExtractOutput(selection, folderName);
        string nsDefault = ComputeDefaultExtractNamespace(selection, folderName);

        List<GridRow> rows =
        [
            new("source-option", sourceOption, true, true, "Readonly"),
            new("source-value", sourceValue, true, true, "Readonly"),
            new("rule-folder-name", folderName, false, false, "Sub-rule folder name"),
            new("output-path", outputPath, true, false, "Where *.g.cs will be generated"),
            new("namespace", nsDefault, false, false, "Default: SelectedClassNamespace.Rules"),
            new("context", string.Empty, false, false, "--context"),
            new("pattern", "**/*.cs", false, false, "--pattern"),
            new("exclude", string.Empty, false, false, "CSV for --exclude"),
            new("tenant", string.Empty, false, false, "--tenant"),
            new("generate-tests", "false", false, false, "true/false"),
            new("validate", "false", false, false, "true/false"),
            new("organize-by-namespace", "false", false, false, "Keep false for same-level folder"),
            new("parallel", "true", false, false, "true/false"),
            new("auto-register", "true", false, false, "Run register right after extract"),
            new("registration-file-name", "MGeneratedRuleRegistrationExtensions.g.cs", false, false, "Default registration file name"),
            new("registration-class-name", "MGeneratedRuleRegistrationExtensions", false, false, "Registration extension class name"),
            new("registration-namespace", string.Empty, false, false, "Default: same as namespace above"),
            new("generate-dispatchers", "true", false, false, "register --generate-dispatchers"),
            new("register-dispatchers", "true", false, false, "register --register-dispatchers"),
            new("include-rule-engine", "true", false, false, "register --include-rule-engine"),
            new("dispatcher-output", string.Empty, false, false, "register --dispatcher-output (optional)"),
            new("dispatcher-namespace", string.Empty, false, false, "register --dispatcher-namespace (optional)"),
            new("dispatcher-overwrite", "true", false, false, "register --dispatcher-overwrite"),
            new("raw", string.Empty, false, false, "Additional raw CLI options")
        ];

        if (!RuleGenGridDialog.TryPrompt("Muonroi RuleGen - Extract", rows, out Dictionary<string, string> values))
        {
            result = new ExtractDialogResult();
            return false;
        }

        result = new ExtractDialogResult
        {
            SourceOption = Get(values, "source-option"),
            SourceValue = Get(values, "source-value"),
            RuleFolderName = Get(values, "rule-folder-name", "Rules"),
            OutputPath = Get(values, "output-path"),
            Namespace = Get(values, "namespace"),
            Context = Get(values, "context"),
            Pattern = Get(values, "pattern", "**/*.cs"),
            Exclude = Get(values, "exclude"),
            Tenant = Get(values, "tenant"),
            GenerateTests = ToBool(Get(values, "generate-tests", "false")),
            Validate = ToBool(Get(values, "validate", "false")),
            OrganizeByNamespace = ToBool(Get(values, "organize-by-namespace", "false")),
            Parallel = ToBool(Get(values, "parallel", "true")),
            AutoRegister = ToBool(Get(values, "auto-register", "true")),
            RegistrationFileName = Get(values, "registration-file-name", "MGeneratedRuleRegistrationExtensions.g.cs"),
            RegistrationClassName = Get(values, "registration-class-name", "MGeneratedRuleRegistrationExtensions"),
            RegistrationNamespace = Get(values, "registration-namespace"),
            GenerateDispatchers = ToBool(Get(values, "generate-dispatchers", "true")),
            RegisterDispatchers = ToBool(Get(values, "register-dispatchers", "true")),
            IncludeRuleEngine = ToBool(Get(values, "include-rule-engine", "true")),
            DispatcherOutput = Get(values, "dispatcher-output"),
            DispatcherNamespace = Get(values, "dispatcher-namespace"),
            DispatcherOverwrite = ToBool(Get(values, "dispatcher-overwrite", "true")),
            Raw = Get(values, "raw")
        };

        return true;
    }
    public static bool TryPromptMergeDialog(SelectionContext selection, out MergeDialogResult result)
    {
        string modeDefault = InferMergeMode(selection);
        string sourceDefault = modeDefault == "json"
            ? selection.Path
            : selection.Kind == SelectionKind.File
                ? Path.GetDirectoryName(selection.Path) ?? selection.WorkingDirectory
                : selection.Path;

        if (selection.Kind == SelectionKind.File &&
            selection.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            sourceDefault = selection.Path;
        }

        string targetDefault = selection.Kind == SelectionKind.File &&
            selection.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                ? selection.Path
                : Path.Combine(selection.WorkingDirectory, modeDefault == "json" ? "MergedFromRuntimeJson.cs" : "MergedFromRules.cs");
        string namespaceDefault = ComputeNamespaceFromOutputPath(selection, targetDefault, outputIsFilePath: true);

        List<GridRow> rows =
        [
            new("mode", modeDefault, true, false, "generated | attribute | json"),
            new("source-path", sourceDefault, true, false, "rules-dir/source-dir/rules-json"),
            new("target-path", targetDefault, true, false, "output class file"),
            new("class", string.Empty, false, false, "--class"),
            new("namespace", namespaceDefault, false, false, "Default: derived from target output"),
            new("context", string.Empty, false, false, "--context"),
            new("pattern", modeDefault == "generated" ? "*.g.cs" : "**/*.cs", false, false, "--pattern"),
            new("exclude", string.Empty, false, false, "--exclude (attribute mode)"),
            new("recursive", "true", false, false, "--recursive"),
            new("parallel", "true", false, false, "--parallel (attribute mode)"),
            new("compile-check", "true", false, false, "--compile-check"),
            new("compile-target", string.Empty, false, false, "--compile-target"),
            new("strategy", "append", false, false, "--strategy (json mode)"),
            new("workflow", string.Empty, false, false, "--workflow (json mode)"),
            new("tenant", string.Empty, false, false, "--tenant (json mode)"),
            new("raw", string.Empty, false, false, "Additional raw CLI options")
        ];

        if (!RuleGenGridDialog.TryPrompt("Muonroi RuleGen - Merge", rows, out Dictionary<string, string> values))
        {
            result = new MergeDialogResult();
            return false;
        }

        result = new MergeDialogResult
        {
            Mode = NormalizeMergeMode(Get(values, "mode", modeDefault)),
            SourcePath = Get(values, "source-path"),
            TargetPath = Get(values, "target-path"),
            ClassName = Get(values, "class"),
            Namespace = Get(values, "namespace"),
            Context = Get(values, "context"),
            Pattern = Get(values, "pattern", modeDefault == "generated" ? "*.g.cs" : "**/*.cs"),
            Exclude = Get(values, "exclude"),
            Recursive = ToBool(Get(values, "recursive", "true")),
            Parallel = ToBool(Get(values, "parallel", "true")),
            CompileCheck = ToBool(Get(values, "compile-check", "true")),
            CompileTarget = Get(values, "compile-target"),
            Strategy = Get(values, "strategy", "append"),
            Workflow = Get(values, "workflow"),
            Tenant = Get(values, "tenant"),
            Raw = Get(values, "raw")
        };

        return true;
    }

    public static string ComputeDefaultExtractOutput(SelectionContext selection, string folderName)
    {
        string baseDirectory = selection.Kind switch
        {
            SelectionKind.Project => Path.GetDirectoryName(selection.Path) ?? selection.WorkingDirectory,
            SelectionKind.File => Path.GetDirectoryName(selection.Path) ?? selection.WorkingDirectory,
            SelectionKind.Folder => selection.Path,
            _ => selection.WorkingDirectory
        };

        string safeFolder = string.IsNullOrWhiteSpace(folderName) ? "Rules" : folderName.Trim();
        return Path.Combine(baseDirectory, safeFolder);
    }

    public static string ComputeDefaultExtractNamespace(SelectionContext selection, string folderName)
    {
        string sourceNamespace = TryReadNamespaceFromSelection(selection);
        if (string.IsNullOrWhiteSpace(sourceNamespace))
        {
            return string.Empty;
        }

        string safeFolder = string.IsNullOrWhiteSpace(folderName) ? "Rules" : folderName.Trim();
        return sourceNamespace + "." + safeFolder;
    }

    public static string ComputeNamespaceFromOutputPath(SelectionContext selection, string outputPath, bool outputIsFilePath)
    {
        string sourceNamespace = TryReadNamespaceFromSelection(selection);
        if (string.IsNullOrWhiteSpace(sourceNamespace))
        {
            return string.Empty;
        }

        string sourceAnchorDirectory = selection.Kind switch
        {
            SelectionKind.File => Path.GetDirectoryName(selection.Path) ?? selection.WorkingDirectory,
            SelectionKind.Folder => selection.Path,
            SelectionKind.Project => Path.GetDirectoryName(selection.Path) ?? selection.WorkingDirectory,
            _ => selection.WorkingDirectory
        };

        string targetDirectory = outputIsFilePath
            ? Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Path.GetFullPath(outputPath)
            : Path.GetFullPath(outputPath);

        return ComputeNamespaceByRelativePath(sourceNamespace, sourceAnchorDirectory, targetDirectory);
    }

    public static bool TryPromptWatchDialog(SelectionContext selection, out WatchDialogResult result)
    {
        string sourceDefault = selection.Kind switch
        {
            SelectionKind.Project => selection.WorkingDirectory,
            SelectionKind.File => selection.WorkingDirectory,
            SelectionKind.Folder => selection.Path,
            _ => selection.WorkingDirectory
        };

        string outputDefault = Path.Combine(sourceDefault, "Rules");
        string sourceNamespace = TryReadNamespaceFromSelection(selection);
        string nsDefault = string.IsNullOrWhiteSpace(sourceNamespace)
            ? string.Empty
            : sourceNamespace + ".Rules";

        List<GridRow> rows =
        [
            new("source-path", sourceDefault, true, false, "watch source directory"),
            new("configure-output", "false", false, false, "true/false"),
            new("output-path", outputDefault, false, false, "used only when configure-output=true"),
            new("namespace", nsDefault, false, false, "--namespace"),
            new("context", string.Empty, false, false, "--context"),
            new("pattern", "**/*.cs", false, false, "--pattern"),
            new("exclude", string.Empty, false, false, "--exclude"),
            new("tenant", string.Empty, false, false, "--tenant"),
            new("generate-tests", "false", false, false, "true/false"),
            new("validate", "false", false, false, "true/false"),
            new("organize-by-namespace", "false", false, false, "true/false"),
            new("parallel", "true", false, false, "true/false"),
            new("raw", string.Empty, false, false, "Additional raw CLI options")
        ];

        if (!RuleGenGridDialog.TryPrompt("Muonroi RuleGen - Watch", rows, out Dictionary<string, string> values))
        {
            result = new WatchDialogResult();
            return false;
        }

        result = new WatchDialogResult
        {
            SourcePath = Get(values, "source-path"),
            ConfigureOutput = ToBool(Get(values, "configure-output", "false")),
            OutputPath = Get(values, "output-path", outputDefault),
            Namespace = Get(values, "namespace"),
            Context = Get(values, "context"),
            Pattern = Get(values, "pattern", "**/*.cs"),
            Exclude = Get(values, "exclude"),
            Tenant = Get(values, "tenant"),
            GenerateTests = ToBool(Get(values, "generate-tests", "false")),
            Validate = ToBool(Get(values, "validate", "false")),
            OrganizeByNamespace = ToBool(Get(values, "organize-by-namespace", "false")),
            Parallel = ToBool(Get(values, "parallel", "true")),
            Raw = Get(values, "raw")
        };

        return true;
    }

    public static IReadOnlyList<string> SplitArguments(string raw)
    {
        List<string> tokens = [];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return tokens;
        }

        StringBuilder current = new();
        bool inQuotes = false;
        for (int i = 0; i < raw.Length; i++)
        {
            char ch = raw[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    private static string TryReadNamespaceFromSelection(SelectionContext selection)
    {
        if (selection.Kind == SelectionKind.File &&
            selection.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(selection.Path))
        {
            string content = File.ReadAllText(selection.Path);
            return TryReadNamespace(content);
        }

        if (selection.Kind == SelectionKind.Folder && Directory.Exists(selection.Path))
        {
            return TryReadNamespaceFromDirectory(selection.Path);
        }

        if (selection.Kind == SelectionKind.Project)
        {
            string projectDirectory = Path.GetDirectoryName(selection.Path) ?? selection.WorkingDirectory;
            if (Directory.Exists(projectDirectory))
            {
                return TryReadNamespaceFromDirectory(projectDirectory);
            }
        }

        return string.Empty;
    }

    private static string TryReadNamespaceFromDirectory(string directoryPath)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(directoryPath, "*.cs", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(file);
                string ns = TryReadNamespace(content);
                if (!string.IsNullOrWhiteSpace(ns))
                {
                    return ns;
                }
            }
        }
        catch
        {
            // ignore scanning errors and fallback to empty
        }

        return string.Empty;
    }

    private static string TryReadNamespace(string content)
    {
        Match m1 = FileScopedNamespaceRegex.Match(content ?? string.Empty);
        if (m1.Success)
        {
            return m1.Groups[1].Value.Trim();
        }

        Match m2 = BlockNamespaceRegex.Match(content ?? string.Empty);
        return m2.Success ? m2.Groups[1].Value.Trim() : string.Empty;
    }

    private static string ComputeNamespaceByRelativePath(string sourceNamespace, string sourceAnchorDirectory, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceNamespace))
        {
            return string.Empty;
        }

        string[] baseParts = sourceNamespace
            .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> namespaceParts = new(baseParts);

        string sourceFullPath = Path.GetFullPath(sourceAnchorDirectory);
        string targetFullPath = Path.GetFullPath(targetDirectory);
        string relative = GetRelativePathCompat(sourceFullPath, targetFullPath);
        string[] segments = relative
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string raw in segments)
        {
            string segment = raw.Trim();
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (namespaceParts.Count > 0)
                {
                    namespaceParts.RemoveAt(namespaceParts.Count - 1);
                }

                continue;
            }

            string normalized = NormalizeNamespaceSegment(segment);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                namespaceParts.Add(normalized);
            }
        }

        return namespaceParts.Count == 0 ? sourceNamespace : string.Join(".", namespaceParts);
    }

    private static string NormalizeNamespaceSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.Empty;
        }

        char[] buffer = segment.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (!char.IsLetterOrDigit(buffer[i]) && buffer[i] != '_')
            {
                buffer[i] = '_';
            }
        }

        string normalized = new string(buffer).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        if (char.IsDigit(normalized[0]))
        {
            normalized = "_" + normalized;
        }

        return normalized;
    }

    private static string GetRelativePathCompat(string basePath, string targetPath)
    {
        string baseFull = Path.GetFullPath(basePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string targetFull = Path.GetFullPath(targetPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Uri baseUri = new(baseFull, UriKind.Absolute);
        Uri targetUri = new(targetFull, UriKind.Absolute);
        Uri relativeUri = baseUri.MakeRelativeUri(targetUri);
        string relative = Uri.UnescapeDataString(relativeUri.ToString());
        return relative.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string InferMergeMode(SelectionContext selection)
    {
        if (selection.Kind == SelectionKind.File &&
            selection.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return "json";
        }

        string folder = selection.Kind == SelectionKind.File
            ? Path.GetDirectoryName(selection.Path) ?? selection.WorkingDirectory
            : selection.Path;
        if (Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.g.cs", SearchOption.AllDirectories).Any())
        {
            return "generated";
        }

        return "attribute";
    }

    private static string NormalizeMergeMode(string mode)
    {
        string normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized is "generated" or "g")
        {
            return "generated";
        }

        if (normalized is "attribute" or "attr" or "source")
        {
            return "attribute";
        }

        if (normalized is "json" or "runtime")
        {
            return "json";
        }

        return "generated";
    }

    private static bool ToBool(string value)
    {
        return string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string Get(Dictionary<string, string> map, string key, string fallback = "")
    {
        return map.TryGetValue(key, out string value) ? value : fallback;
    }

    private sealed class GridRow(string key, string value, bool required, bool readOnly, string hint)
    {
        public string Key { get; } = key;
        public string Value { get; } = value;
        public bool Required { get; } = required;
        public bool ReadOnly { get; } = readOnly;
        public string Hint { get; } = hint;
    }

    private sealed class ProcessingDialogScope(ProcessingDialogForm form) : IDisposable
    {
        private readonly ProcessingDialogForm _form = form;

        public void Dispose()
        {
            try
            {
                if (_form.IsDisposed)
                {
                    return;
                }

                if (_form.InvokeRequired)
                {
                    _form.BeginInvoke(new Action(() =>
                    {
                        _form.Close();
                        _form.Dispose();
                    }));
                    return;
                }

                _form.Close();
                _form.Dispose();
            }
            catch
            {
                // best-effort cleanup only
            }
        }
    }

    private sealed class ProcessingDialogForm : Form
    {
        public ProcessingDialogForm(string title, string message)
        {
            Text = title;
            Width = 520;
            Height = 150;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;

            Label messageLabel = new()
            {
                Left = 18,
                Top = 16,
                Width = 470,
                Height = 28,
                Text = string.IsNullOrWhiteSpace(message) ? "Processing..." : message
            };

            ProgressBar progress = new()
            {
                Left = 18,
                Top = 54,
                Width = 470,
                Height = 18,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 35
            };

            Label hint = new()
            {
                Left = 18,
                Top = 82,
                Width = 470,
                Height = 20,
                Text = "Muonroi RuleGen is running. See Output > Muonroi RuleGen for live logs."
            };

            Controls.Add(messageLabel);
            Controls.Add(progress);
            Controls.Add(hint);
            ApplyVisualStudioTheme(this, null);
        }
    }

    private static class RuleGenGridDialog
    {
        public static bool TryPrompt(string title, IReadOnlyList<GridRow> rows, out Dictionary<string, string> values)
        {
            using GridDialogForm form = new(title, rows);
            if (form.ShowDialog() != DialogResult.OK)
            {
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return false;
            }

            values = form.Values;
            return true;
        }

        private sealed class GridDialogForm : Form
        {
            private readonly IReadOnlyList<GridRow> _rows;
            private readonly DataGridView _grid;
            public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

            public GridDialogForm(string title, IReadOnlyList<GridRow> rows)
            {
                _rows = rows;

                Text = title;
                Width = 1150;
                Height = 720;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                StartPosition = FormStartPosition.CenterParent;

                _grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AutoGenerateColumns = false,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    RowHeadersVisible = false,
                    SelectionMode = DataGridViewSelectionMode.CellSelect
                };

                _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Option", Width = 280, ReadOnly = true });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", Width = 540, ReadOnly = false });
                _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", Width = 320, ReadOnly = true });

                Label header = new()
                {
                    Dock = DockStyle.Top,
                    Height = 42,
                    Padding = new Padding(12, 10, 12, 0),
                    Text = "Fill options below. Required fields are marked with *."
                };

                foreach (GridRow row in rows)
                {
                    string displayName = HumanizeKey(row.Key);
                    if (row.Required)
                    {
                        displayName += " *";
                    }

                    int idx = _grid.Rows.Add(displayName, row.Value, row.Hint);
                    if (row.ReadOnly)
                    {
                        _grid.Rows[idx].Cells[1].ReadOnly = true;
                        _grid.Rows[idx].Cells[1].Style.BackColor = Color.Gainsboro;
                    }
                    else if (LooksBoolean(row))
                    {
                        DataGridViewComboBoxCell combo = new()
                        {
                            FlatStyle = FlatStyle.Flat
                        };
                        combo.Items.Add("true");
                        combo.Items.Add("false");
                        combo.Value = NormalizeBoolCell(row.Value);
                        _grid.Rows[idx].Cells[1] = combo;
                    }
                }

                Panel footer = new() { Dock = DockStyle.Bottom, Height = 44 };
                Button okButton = new() { Text = "OK", Width = 90, Height = 28, Left = 940, Top = 8 };
                Button cancelButton = new() { Text = "Cancel", Width = 90, Height = 28, Left = 1040, Top = 8 };
                okButton.Click += (_, _) =>
                {
                    if (!ValidateAndCollect())
                    {
                        return;
                    }

                    DialogResult = DialogResult.OK;
                    Close();
                };
                cancelButton.Click += (_, _) =>
                {
                    DialogResult = DialogResult.Cancel;
                    Close();
                };
                footer.Controls.Add(okButton);
                footer.Controls.Add(cancelButton);

                ApplyVisualStudioTheme(this, _grid);
                Controls.Add(header);
                Controls.Add(_grid);
                Controls.Add(footer);
            }

            private bool ValidateAndCollect()
            {
                Values.Clear();
                for (int i = 0; i < _rows.Count; i++)
                {
                    string key = _rows[i].Key;
                    string value = (_grid.Rows[i].Cells[1].Value?.ToString() ?? string.Empty).Trim();
                    if (_rows[i].Required && string.IsNullOrWhiteSpace(value))
                    {
                        MessageBox.Show(this, $"'{key}' is required.", "Muonroi RuleGen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _grid.CurrentCell = _grid.Rows[i].Cells[1];
                        return false;
                    }

                    Values[key] = value;
                }

                return true;
            }

            private static bool LooksBoolean(GridRow row)
            {
                string value = row.Value ?? string.Empty;
                if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return (row.Hint ?? string.Empty).IndexOf("true/false", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static object NormalizeBoolCell(string value)
            {
                return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
            }
        }
    }

    private static string HumanizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        string[] parts = key.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return key;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            parts[i] = p.Length > 1
                ? char.ToUpperInvariant(p[0]) + p.Substring(1)
                : p.ToUpperInvariant();
        }

        return string.Join(" ", parts);
    }

    private static void ApplyVisualStudioTheme(Form form, DataGridView grid)
    {
        try
        {
            Color bg = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            Color fg = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            Color inputBg = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxBackgroundColorKey);
            Color inputFg = VSColorTheme.GetThemedColor(EnvironmentColors.ComboBoxTextColorKey);
            Color selectionBg = VSColorTheme.GetThemedColor(EnvironmentColors.SystemHighlightColorKey);
            Color selectionFg = VSColorTheme.GetThemedColor(EnvironmentColors.SystemHighlightTextColorKey);

            form.BackColor = bg;
            form.ForeColor = fg;

            foreach (Control c in form.Controls)
            {
                if (c is Button button)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = inputBg;
                    button.ForeColor = inputFg;
                    continue;
                }

                c.BackColor = bg;
                c.ForeColor = fg;
            }

            if (grid is null)
            {
                return;
            }

            grid.BackgroundColor = bg;
            grid.GridColor = inputBg;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = bg;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = fg;
            grid.DefaultCellStyle.BackColor = inputBg;
            grid.DefaultCellStyle.ForeColor = inputFg;
            grid.DefaultCellStyle.SelectionBackColor = selectionBg;
            grid.DefaultCellStyle.SelectionForeColor = selectionFg;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Blend(inputBg, bg, 0.85f);
            grid.AlternatingRowsDefaultCellStyle.ForeColor = inputFg;
        }
        catch
        {
            // fallback to default WinForms styling if VS theme cannot be read
        }
    }

    private static Color Blend(Color a, Color b, float amountA)
    {
        float amountB = 1f - amountA;
        int r = (int)(a.R * amountA + b.R * amountB);
        int g = (int)(a.G * amountA + b.G * amountB);
        int bC = (int)(a.B * amountA + b.B * amountB);
        return Color.FromArgb(255, Clamp(r), Clamp(g), Clamp(bC));
    }

    private static int Clamp(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 255)
        {
            return 255;
        }

        return value;
    }
}
