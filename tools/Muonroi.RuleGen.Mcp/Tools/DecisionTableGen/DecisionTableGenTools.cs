using System.ComponentModel;
using ModelContextProtocol.Server;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Serializers;
using Muonroi.RuleEngine.DecisionTable.Validators;
using Muonroi.RuleGen.Mcp.Models;
using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleGen.Mcp.Tools.DecisionTableGen;

[McpServerToolType]
public sealed class ImportExcelTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_dt_import_excel")]
    public async Task<string> ExecuteAsync(string sourcePath, string outputPath, string? tenantId = null, string? workingDirectory = null, CancellationToken ct = default)
    {
        string cwd = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : Path.GetFullPath(workingDirectory);
        string resolvedSource = Path.GetFullPath(sourcePath, cwd);
        string resolvedOutput = Path.GetFullPath(outputPath, cwd);

        ExcelToDecisionTableConverter converter = new();
        DecisionTableModel table = converter.Convert(resolvedSource, tenantId);
        string json = DecisionTableJsonSerializer.Serialize(table);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutput)!);
        await File.WriteAllTextAsync(resolvedOutput, json, ct);

        DecisionTableImportResult result = new(table.Name, table.Rows.Count, table.InputColumns.Count + table.OutputColumns.Count, resolvedOutput);
        return jsonService.Serialize(result);
    }
}

[McpServerToolType]
public sealed class ValidateDecisionTableTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_dt_validate")]
    public async Task<string> ExecuteAsync(string sourcePath, string? workingDirectory = null, CancellationToken ct = default)
    {
        string cwd = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : Path.GetFullPath(workingDirectory);
        string resolvedSource = Path.GetFullPath(sourcePath, cwd);
        string json = await File.ReadAllTextAsync(resolvedSource, ct);
        DecisionTableModel table = DecisionTableJsonSerializer.Deserialize(json);
        DecisionTableValidator validator = new();
        ValidationResult validation = validator.Validate(table);
        return jsonService.Serialize(new DecisionTableValidationResult(validation.IsValid, validation.Errors, validation.Warnings ?? []));
    }
}

[McpServerToolType]
public sealed class ExportDecisionTableJsonTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_dt_export_json")]
    public async Task<string> ExecuteAsync(string sourcePath, string outputPath, string? workingDirectory = null, CancellationToken ct = default)
    {
        string cwd = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : Path.GetFullPath(workingDirectory);
        string resolvedSource = Path.GetFullPath(sourcePath, cwd);
        string resolvedOutput = Path.GetFullPath(outputPath, cwd);

        DecisionTableToJsonConverter converter = new();
        DecisionTableModel table = DecisionTableJsonSerializer.Deserialize(await File.ReadAllTextAsync(resolvedSource, ct));
        string workflowJson = converter.Convert(table);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutput)!);
        await File.WriteAllTextAsync(resolvedOutput, workflowJson, ct);

        return jsonService.Serialize(new DecisionTableExportResult(resolvedOutput, table.Rows.Count));
    }
}

[McpServerToolType]
public sealed class ExportDecisionTableDmnTool(IMJsonSerializeService jsonService)
{
    [McpServerTool(Name = "muonroi_dt_export_dmn")]
    public async Task<string> ExecuteAsync(string sourcePath, string outputPath, string? workingDirectory = null, CancellationToken ct = default)
    {
        string cwd = string.IsNullOrWhiteSpace(workingDirectory) ? Directory.GetCurrentDirectory() : Path.GetFullPath(workingDirectory);
        string resolvedSource = Path.GetFullPath(sourcePath, cwd);
        string resolvedOutput = Path.GetFullPath(outputPath, cwd);

        DecisionTableModel table = DecisionTableJsonSerializer.Deserialize(await File.ReadAllTextAsync(resolvedSource, ct));
        string dmn = DecisionTableXmlSerializer.SerializeToDmnXml(table);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutput)!);
        await File.WriteAllTextAsync(resolvedOutput, dmn, ct);

        return jsonService.Serialize(new DecisionTableExportResult(resolvedOutput, table.Rows.Count));
    }
}
