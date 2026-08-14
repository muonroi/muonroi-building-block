namespace Muonroi.RuleGen.VisualStudio;

internal enum SelectionKind
{
    Unknown = 0,
    File = 1,
    Folder = 2,
    Project = 3
}

internal sealed class SelectionContext
{
    public SelectionKind Kind { get; set; }
    public string Path { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

internal static class SelectedItemResolver
{
    public static bool TryGetSelection(DTE2 dte, out SelectionContext selection)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        selection = null;

        if (TryGetSelectionFromVsMonitorSelection(out selection))
        {
            return true;
        }

        if (TryGetSelectionFromSolutionExplorer(dte, out selection))
        {
            return true;
        }

        if (TryGetSelectionFromActiveDocument(dte, out selection))
        {
            return true;
        }

        return false;
    }

    public static bool TryDescribeSelection(DTE2 dte, out string description)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (TryGetSelection(dte, out SelectionContext selection) && selection is not null)
        {
            description = $"{selection.Source} kind={selection.Kind}, path={selection.Path}, wd={selection.WorkingDirectory}";
            return true;
        }

        int selectedCount = 0;
        try
        {
            if (dte.ToolWindows.SolutionExplorer.SelectedItems is SelectedItems selectedItems)
            {
                selectedCount = selectedItems.Count;
            }
        }
        catch
        {
            // ignore
        }

        string activeDoc = "<none>";
        try
        {
            activeDoc = dte.ActiveDocument?.FullName ?? "<none>";
        }
        catch
        {
            // ignore
        }

        description = $"unresolved selectedItems.Count={selectedCount}, activeDocument={activeDoc}";
        return false;
    }

    private static bool TryGetSelectionFromVsMonitorSelection(out SelectionContext selection)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        selection = null;

        IVsMonitorSelection monitorSelection = Package.GetGlobalService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
        if (monitorSelection == null)
        {
            return false;
        }

        IntPtr hierarchyPtr = IntPtr.Zero;
        IntPtr selectionContainerPtr = IntPtr.Zero;

        try
        {
            int hr = monitorSelection.GetCurrentSelection(out hierarchyPtr, out uint itemId, out IVsMultiItemSelect multiItemSelect, out selectionContainerPtr);
            if (ErrorHandler.Failed(hr) || hierarchyPtr == IntPtr.Zero || itemId == VSConstants.VSITEMID_NIL)
            {
                return false;
            }

            if (multiItemSelect != null)
            {
                return false;
            }

            IVsHierarchy hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;
            if (hierarchy == null)
            {
                return false;
            }

            string path = TryGetPathFromHierarchy(hierarchy, itemId);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (File.Exists(path))
            {
                selection = new SelectionContext
                {
                    Kind = SelectionKind.File,
                    Path = path,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory,
                    Source = "vs-monitor-selection"
                };
                return true;
            }

            if (Directory.Exists(path))
            {
                selection = new SelectionContext
                {
                    Kind = SelectionKind.Folder,
                    Path = path,
                    WorkingDirectory = path,
                    Source = "vs-monitor-selection"
                };
                return true;
            }

            return false;
        }
        finally
        {
            if (selectionContainerPtr != IntPtr.Zero)
            {
                Marshal.Release(selectionContainerPtr);
            }

            if (hierarchyPtr != IntPtr.Zero)
            {
                Marshal.Release(hierarchyPtr);
            }
        }
    }

    private static string TryGetPathFromHierarchy(IVsHierarchy hierarchy, uint itemId)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (hierarchy is IVsProject project)
        {
            int hrDoc = project.GetMkDocument(itemId, out string mkDocument);
            if (!ErrorHandler.Failed(hrDoc) && !string.IsNullOrWhiteSpace(mkDocument))
            {
                return mkDocument;
            }
        }

        int hrCanonical = hierarchy.GetCanonicalName(itemId, out string canonicalName);
        if (!ErrorHandler.Failed(hrCanonical) && !string.IsNullOrWhiteSpace(canonicalName))
        {
            return canonicalName;
        }

        int hrExtObject = hierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_ExtObject, out object extObject);
        if (!ErrorHandler.Failed(hrExtObject) && extObject is ProjectItem projectItem)
        {
            string itemPath = TryGetProjectItemPath(projectItem);
            if (!string.IsNullOrWhiteSpace(itemPath))
            {
                return itemPath;
            }
        }

        if (!ErrorHandler.Failed(hrExtObject) && extObject is Project extProject)
        {
            if (!string.IsNullOrWhiteSpace(extProject.FullName))
            {
                return extProject.FullName;
            }

            string fullPath = TryGetProjectPropertyString(extProject, "FullPath");
            if (!string.IsNullOrWhiteSpace(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static bool TryGetSelectionFromSolutionExplorer(DTE2 dte, out SelectionContext selection)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        selection = null;

        if (dte.ToolWindows.SolutionExplorer.SelectedItems is not SelectedItems selectedItems || selectedItems.Count < 1)
        {
            return false;
        }

        UIHierarchyItem item = selectedItems.Item(1) as UIHierarchyItem;
        if (item == null)
        {
            return false;
        }

        if (TryGetSelectionFromHierarchyItem(item, out selection) && selection is not null)
        {
            selection.Source = "solution-explorer";
            return true;
        }

        return false;
    }

    private static bool TryGetSelectionFromHierarchyItem(UIHierarchyItem item, out SelectionContext selection)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        selection = null;

        if (item.Object is ProjectItem projectItem)
        {
            string itemPath = TryGetProjectItemPath(projectItem);
            if (string.IsNullOrWhiteSpace(itemPath))
            {
                return false;
            }

            if (File.Exists(itemPath))
            {
                selection = new SelectionContext
                {
                    Kind = SelectionKind.File,
                    Path = itemPath,
                    WorkingDirectory = Path.GetDirectoryName(itemPath) ?? Environment.CurrentDirectory
                };
                return true;
            }

            if (Directory.Exists(itemPath))
            {
                selection = new SelectionContext
                {
                    Kind = SelectionKind.Folder,
                    Path = itemPath,
                    WorkingDirectory = itemPath
                };
                return true;
            }

            return false;
        }

        if (item.Object is Project project)
        {
            string projectFile = project.FullName;
            if (!string.IsNullOrWhiteSpace(projectFile) && File.Exists(projectFile))
            {
                string projectDir = Path.GetDirectoryName(projectFile) ?? Environment.CurrentDirectory;
                selection = new SelectionContext
                {
                    Kind = SelectionKind.Project,
                    Path = projectFile,
                    WorkingDirectory = projectDir
                };
                return true;
            }

            string projectDirProperty = TryGetProjectPropertyString(project, "FullPath");
            if (!string.IsNullOrWhiteSpace(projectDirProperty) && Directory.Exists(projectDirProperty))
            {
                selection = new SelectionContext
                {
                    Kind = SelectionKind.Folder,
                    Path = projectDirProperty,
                    WorkingDirectory = projectDirProperty
                };
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSelectionFromActiveDocument(DTE2 dte, out SelectionContext selection)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        selection = null;

        Document activeDocument;
        try
        {
            activeDocument = dte.ActiveDocument;
        }
        catch
        {
            return false;
        }

        if (activeDocument == null || string.IsNullOrWhiteSpace(activeDocument.FullName))
        {
            return false;
        }

        string fullPath = activeDocument.FullName;
        if (!File.Exists(fullPath))
        {
            return false;
        }

        selection = new SelectionContext
        {
            Kind = SelectionKind.File,
            Path = fullPath,
            WorkingDirectory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory,
            Source = "active-document"
        };

        return true;
    }

    private static string TryGetProjectItemPath(ProjectItem projectItem)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            if (projectItem.FileCount > 0)
            {
                string byFileNames = projectItem.FileNames[1];
                if (!string.IsNullOrWhiteSpace(byFileNames))
                {
                    return byFileNames;
                }
            }
        }
        catch
        {
            // ignore COM edge cases
        }

        string byFullPath = TryGetPropertyString(projectItem.Properties, "FullPath");
        if (!string.IsNullOrWhiteSpace(byFullPath))
        {
            return byFullPath;
        }

        string byLocalPath = TryGetPropertyString(projectItem.Properties, "LocalPath");
        if (!string.IsNullOrWhiteSpace(byLocalPath))
        {
            return byLocalPath;
        }

        return null;
    }

    private static string TryGetProjectPropertyString(Project project, string propertyName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            Property property = project.Properties?.Item(propertyName);
            object value = property?.Value;
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string TryGetPropertyString(Properties properties, string propertyName)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        try
        {
            Property property = properties?.Item(propertyName);
            object value = property?.Value;
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
