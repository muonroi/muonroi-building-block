using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Converters;

/// <summary>
/// Imports decision table from Excel worksheet.
/// Header format: in:Age, in:Country, out:Approved, out:Score
/// </summary>
public sealed class ExcelToDecisionTableConverter
{
    /// <summary>
    /// Loads a decision table from an Excel file path.
    /// </summary>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <param name="tenantId">Optional tenant identifier to attach to the table.</param>
    /// <returns>Parsed decision table.</returns>
    public DecisionTableModel Convert(string filePath, string? tenantId = null)
    {
        using FileStream stream = File.OpenRead(filePath);
        string name = Path.GetFileNameWithoutExtension(filePath);
        return Convert(stream, name, tenantId);
    }

    /// <summary>
    /// Loads a decision table from an Excel stream.
    /// </summary>
    /// <param name="stream">Excel stream to read.</param>
    /// <param name="tableName">Name assigned to the table.</param>
    /// <param name="tenantId">Optional tenant identifier to attach to the table.</param>
    /// <returns>Parsed decision table.</returns>
    public static DecisionTableModel Convert(Stream stream, string tableName, string? tenantId = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);

        System.Data.DataSet ds = reader.AsDataSet();
        if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
        {
            throw new MConfigurationException("Excel file is empty.");
        }

        System.Data.DataTable sheet = ds.Tables[0];
        System.Data.DataRow header = sheet.Rows[0];
        List<(string Name, bool IsInput)> columns = [];
        for (int i = 0; i < sheet.Columns.Count; i++)
        {
            string raw = System.Convert.ToString(header[i], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (raw.StartsWith("in:", StringComparison.OrdinalIgnoreCase))
            {
                columns.Add((raw[3..].Trim(), true));
            }
            else if (raw.StartsWith("out:", StringComparison.OrdinalIgnoreCase))
            {
                columns.Add((raw[4..].Trim(), false));
            }
            else
            {
                throw new MConfigurationException(
                    $"Invalid header '{raw}'. Use 'in:<name>' or 'out:<name>' format.");
            }
        }

        DecisionTableModel table = new()
        {
            Name = tableName,
            TenantId = tenantId,
            InputColumns = [.. columns.Where(x => x.IsInput)
                .Select(x =>
                {
                    DecisionTableColumn column = new() {
                        Name = x.Name,
                        Label = x.Name,
                        DataType = "string"
                    };
                    return column;
                })],
            OutputColumns = [.. columns.Where(x => !x.IsInput)
                .Select(x =>
                {
                    DecisionTableColumn column = new() {
                        Name = x.Name,
                        Label = x.Name,
                        DataType = "string"
                    };
                    return column;
                })]
        };

        int inputCount = table.InputColumns.Count;
        int outputCount = table.OutputColumns.Count;

        for (int rowIndex = 1; rowIndex < sheet.Rows.Count; rowIndex++)
        {
            System.Data.DataRow sheetRow = sheet.Rows[rowIndex];
            if (sheetRow.ItemArray.All(x => string.IsNullOrWhiteSpace(System.Convert.ToString(x, CultureInfo.InvariantCulture))))
            {
                continue;
            }

            DecisionTableRow row = new()
            {
                Order = rowIndex
            };

            int colIndex = 0;
            for (int i = 0; i < inputCount; i++, colIndex++)
            {
                DecisionTableCell item = new()
                {
                    ColumnId = table.InputColumns[i].Id,
                    Expression = System.Convert.ToString(sheetRow[colIndex], CultureInfo.InvariantCulture)?.Trim() ?? "-"
                };
                row.InputCells.Add(item);
            }

            for (int i = 0; i < outputCount; i++, colIndex++)
            {
                DecisionTableCell item = new()
                {
                    ColumnId = table.OutputColumns[i].Id,
                    Expression = System.Convert.ToString(sheetRow[colIndex], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty
                };
                row.OutputCells.Add(item);
            }

            table.Rows.Add(row);
        }

        return table;
    }
}
