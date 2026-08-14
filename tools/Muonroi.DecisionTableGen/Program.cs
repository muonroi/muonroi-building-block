namespace Muonroi.DecisionTableGen;

internal static class Program
{
    private const string sourceKey = "source";

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].Trim().ToLowerInvariant();
        IReadOnlyDictionary<string, string> options = ParseOptions([.. args.Skip(1)]);

        return command switch
        {
            "import-excel" => RunImportExcel(options),
            "validate" => RunValidate(options),
            "export-json" => RunExportJson(options),
            "export-dmn" => RunExportDmn(options),
            _ => Fail($"Unknown command '{command}'.")
        };
    }

    private static int RunImportExcel(IReadOnlyDictionary<string, string> options)
    {
        if (!TryGetOption(options, sourceKey, out string? sourcePath) ||
            !TryGetOption(options, "output", out string? outputPath))
        {
            return Fail("Missing required options --source and --output.");
        }

        if (!File.Exists(sourcePath))
        {
            return Fail($"File not found: {sourcePath}");
        }

        string? tenant = options.TryGetValue("tenant", out string? tenantId) ? tenantId : null;
        ExcelToDecisionTableConverter importer = new();
        _ = new DecisionTableJsonSerializer();
        RuleEngine.DecisionTable.Models.DecisionTable table = importer.Convert(sourcePath, tenant);
        string json = DecisionTableJsonSerializer.Serialize(table);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Imported decision table '{table.Name}' with {table.Rows.Count} row(s).");
        return 0;
    }

    private static int RunValidate(IReadOnlyDictionary<string, string> options)
    {
        if (!TryGetOption(options, sourceKey, out string? sourcePath))
        {
            return Fail("Missing required option --source.");
        }

        if (!File.Exists(sourcePath))
        {
            return Fail($"File not found: {sourcePath}");
        }

        _ = new
        DecisionTableJsonSerializer();
        DecisionTableValidator validator = new();
        RuleEngine.DecisionTable.Models.DecisionTable table = DecisionTableJsonSerializer.Deserialize(File.ReadAllText(sourcePath));
        ValidationResult result = validator.Validate(table);

        if (!result.IsValid)
        {
            Console.Error.WriteLine("Validation failed:");
            foreach (string error in result.Errors)
            {
                Console.Error.WriteLine($"- {error}");
            }

            return 2;
        }

        Console.WriteLine("Validation passed.");
        if (result.Warnings is { Count: > 0 })
        {
            Console.WriteLine("Warnings:");
            foreach (string warning in result.Warnings)
            {
                Console.WriteLine($"- {warning}");
            }
        }

        return 0;
    }

    private static int RunExportJson(IReadOnlyDictionary<string, string> options)
    {
        if (!TryGetOption(options, sourceKey, out string? sourcePath) ||
            !TryGetOption(options, "output", out string? outputPath))
        {
            return Fail("Missing required options --source and --output.");
        }

        if (!File.Exists(sourcePath))
        {
            return Fail($"File not found: {sourcePath}");
        }

        _ = new
        DecisionTableJsonSerializer();
        DecisionTableToJsonConverter converter = new();
        RuleEngine.DecisionTable.Models.DecisionTable table = DecisionTableJsonSerializer.Deserialize(File.ReadAllText(sourcePath));
        string workflowJson = converter.Convert(table);
        File.WriteAllText(outputPath, workflowJson);
        Console.WriteLine($"Exported workflow JSON to '{outputPath}'.");
        return 0;
    }

    private static int RunExportDmn(IReadOnlyDictionary<string, string> options)
    {
        if (!TryGetOption(options, sourceKey, out string? sourcePath) ||
            !TryGetOption(options, "output", out string? outputPath))
        {
            return Fail("Missing required options --source and --output.");
        }

        if (!File.Exists(sourcePath))
        {
            return Fail($"File not found: {sourcePath}");
        }

        _ = new
        DecisionTableJsonSerializer();
        _ = new DecisionTableXmlSerializer();
        RuleEngine.DecisionTable.Models.DecisionTable table = DecisionTableJsonSerializer.Deserialize(File.ReadAllText(sourcePath));
        string dmn = DecisionTableXmlSerializer.SerializeToDmnXml(table);
        File.WriteAllText(outputPath, dmn);
        Console.WriteLine($"Exported DMN XML to '{outputPath}'.");
        return 0;
    }

    private static bool TryGetOption(IReadOnlyDictionary<string, string> options, string key, out string value)
    {
        if (options.TryGetValue(key, out string? found) && !string.IsNullOrWhiteSpace(found))
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string key = args[i][2..];
            string value = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : "true";
            options[key] = value;
        }

        return options;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Muonroi.DecisionTableGen");
        Console.WriteLine("Usage:");
        Console.WriteLine("  muonroi-dt import-excel --source <file.xlsx> --output <table.json> [--tenant <tenantId>]");
        Console.WriteLine("  muonroi-dt validate --source <table.json>");
        Console.WriteLine("  muonroi-dt export-json --source <table.json> --output <workflow.json>");
        Console.WriteLine("  muonroi-dt export-dmn --source <table.json> --output <workflow.dmn.xml>");
    }
}
