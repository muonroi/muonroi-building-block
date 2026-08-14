namespace Muonroi.RuleGen.Mcp.Tests;

internal sealed class TestWorkspace : IDisposable
{
    public string RootPath { get; } = Path.Combine(
        Path.GetTempPath(),
        "muonroi-rulegen-mcp-tests",
        Guid.NewGuid().ToString("N"));

    public TestWorkspace()
    {
        Directory.CreateDirectory(RootPath);
    }

    public string WriteFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(RootPath, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public string WriteMinimalExcel(string relativePath, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        string fullPath = Path.Combine(RootPath, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(fullPath);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);

        WriteZipEntry(
            archive,
            "[Content_Types].xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
            </Types>
            """);
        WriteZipEntry(
            archive,
            "_rels/.rels",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
            </Relationships>
            """);
        WriteZipEntry(
            archive,
            "xl/workbook.xml",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                      xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="Sheet1" sheetId="1" r:id="rId1" />
              </sheets>
            </workbook>
            """);
        WriteZipEntry(
            archive,
            "xl/_rels/workbook.xml.rels",
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
            </Relationships>
            """);
        WriteZipEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(rows));

        return fullPath;
    }

    public string GetRepoRelativePath(string relativePathFromRepoRoot)
    {
        return Path.Combine(FindRepoRoot(), relativePathFromRepoRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp test workspaces.
        }
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Muonroi.BuildingBlock.sln")))
            {
                return current;
            }

            string? parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        throw new DirectoryNotFoundException("Cannot locate muonroi-building-block repository root for MCP tests.");
    }

    private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName);
        using StreamWriter writer = new(entry.Open());
        writer.Write(content);
    }

    private static string BuildWorksheetXml(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        IEnumerable<string> rowXml = rows.Select((row, rowIndex) =>
        {
            IEnumerable<string> cells = row.Select((value, columnIndex) =>
            {
                string cellRef = $"{GetColumnName(columnIndex)}{rowIndex + 1}";
                return $"""<c r="{cellRef}" t="inlineStr"><is><t>{EscapeXml(value)}</t></is></c>""";
            });

            return $"""<row r="{rowIndex + 1}">{string.Join(string.Empty, cells)}</row>""";
        });

        return
            $$"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>{{string.Join(string.Empty, rowXml)}}</sheetData>
            </worksheet>
            """;
    }

    private static string GetColumnName(int index)
    {
        int value = index;
        string columnName = string.Empty;
        do
        {
            columnName = (char)('A' + (value % 26)) + columnName;
            value = (value / 26) - 1;
        }
        while (value >= 0);

        return columnName;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
