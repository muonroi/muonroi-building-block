namespace Muonroi.RuleEngine.SourceGen.Tests;

/// <summary>
/// Covers catalog metadata and FEEL expression generation paths.
/// </summary>
public sealed class SourceGeneratorCatalogTests
{
    /// <summary>
    /// Ensures catalog metadata is emitted into the generated manifest provider.
    /// </summary>
    [Fact]
    public void ExtractAsRuleGenerator_ShouldEmitCatalogMetadataIntoManifest()
    {
        string source = @"
using System;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
}

public class MRuleCatalogEntryAttribute : Attribute
{
    public string DisplayName { get; set; }
    public string Category { get; set; }
    public string Icon { get; set; }
    public string[] Tags { get; set; }
    public string Description { get; set; }
    public bool IsPaletteVisible { get; set; } = true;
}

public sealed class FactBag
{
    public void Set<T>(string key, T value) {}
}

public sealed class OrderContext
{
    public decimal Amount { get; set; }
}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_CATALOG"")]
    [MRuleCatalogEntry(DisplayName = ""Validate Order"", Category = ""Orders"", Icon = ""pi-check"", Tags = new[] { ""order"", ""validation"" }, Description = ""Checks the order."", IsPaletteVisible = false)]
    public bool Validate(OrderContext context, FactBag facts)
    {
        return context.Amount > 0;
    }
}
";

        GeneratorDriverRunResult runResult = RunGenerator(source);
        string generated = string.Join("\n\n", GetGeneratedSources(runResult).Select(item => item.SourceText.ToString()));

        Assert.Contains("internal static class RuleCatalogRegistration", generated);
        Assert.Contains("internal sealed class GeneratedManifestProvider", generated);
        Assert.Contains("DisplayName = @\"Validate Order\"", generated);
        Assert.Contains("Category = @\"Orders\"", generated);
        Assert.Contains("Icon = @\"pi-check\"", generated);
        Assert.Contains("Tags = new string[] { @\"order\", @\"validation\" }", generated);
        Assert.Contains("IsPaletteVisible = false", generated);
    }

    /// <summary>
    /// Ensures Expression= emits the FEEL compiler path.
    /// </summary>
    [Fact]
    public void ExtractAsRuleGenerator_ShouldEmitFeelCompilerBranchForExpressionShortcut()
    {
        string source = @"
using System;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
    public string Expression { get; set; }
}

public sealed class FactBag
{
    public System.Collections.Generic.IEnumerable<string> Keys => Array.Empty<string>();
    public T Get<T>(string key) => default;
}

public sealed class OrderContext
{
    public decimal Amount { get; set; }
}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_EXPRESSION"", Expression = ""amount > 0"")]
    public bool Validate(OrderContext context, FactBag facts)
    {
        return false;
    }
}
";

        GeneratorDriverRunResult runResult = RunGenerator(source);
        string generated = string.Join("\n\n", GetGeneratedSources(runResult).Select(item => item.SourceText.ToString()));

        Assert.Contains("FeelExpressionCompiler.Compile", generated);
        Assert.Contains("BuildFeelVariables", generated);
    }

    /// <summary>
    /// Ensures invalid FEEL expressions surface MRG010.
    /// </summary>
    [Fact]
    public void ExtractAsRuleGenerator_ShouldReportMrg010ForInvalidFeelExpression()
    {
        string source = @"
using System;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
    public string Expression { get; set; }
}

public sealed class FactBag {}
public sealed class OrderContext {}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_INVALID_FEEL"", Expression = ""amount >"")]
    public bool Validate(OrderContext context, FactBag facts)
    {
        return true;
    }
}
";

        GeneratorDriverRunResult runResult = RunGenerator(source);

        Assert.Contains(GetDiagnostics(runResult), diagnostic => diagnostic.Id == "MRG010");
    }

    /// <summary>
    /// Ensures valid FEEL expressions do not produce MRG010.
    /// </summary>
    [Fact]
    public void ExtractAsRuleGenerator_ShouldNotReportMrg010ForValidFeelExpression()
    {
        string source = @"
using System;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
    public string Expression { get; set; }
}

public sealed class FactBag {}
public sealed class OrderContext {}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_VALID_FEEL"", Expression = ""amount > 0 and status = 'vip'"")]
    public bool Validate(OrderContext context, FactBag facts)
    {
        return true;
    }
}
";

        GeneratorDriverRunResult runResult = RunGenerator(source);

        Assert.DoesNotContain(GetDiagnostics(runResult), diagnostic => diagnostic.Id == "MRG010");
    }

    /// <summary>
    /// Ensures the dedicated catalog registration generator emits the expected generated file.
    /// </summary>
    [Fact]
    public void RuleCatalogRegistrationGenerator_ShouldEmitRegistrationFile()
    {
        string source = @"
using System;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
}

public sealed class FactBag {}
public sealed class OrderContext {}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_REGISTRATION"")]
    public bool Validate(OrderContext context, FactBag facts)
    {
        return true;
    }
}
";

        GeneratorDriverRunResult runResult = RunGenerator(source);
        GeneratedSourceResult registrationSource = GetGeneratedSources(runResult)
            .Single(item => item.HintName == "RuleCatalogRegistration.g.cs");

        string generated = registrationSource.SourceText.ToString();
        Assert.Contains("RuleCatalogRegistration", generated);
        Assert.Contains("GeneratedManifestProvider", generated);
        Assert.Contains("RULE_REGISTRATION", generated);
    }

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        Compilation compilation = CreateCompilation(source);
        IIncrementalGenerator[] generators =
        [
            new ExtractAsRuleGenerator(),
            new RuleCatalogRegistrationGenerator()
        ];

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators: generators.Select(generator => generator.AsSourceGenerator()));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        return driver.GetRunResult();
    }

    private static ImmutableArray<GeneratedSourceResult> GetGeneratedSources(GeneratorDriverRunResult runResult)
    {
        return [.. runResult.Results.SelectMany(result => result.GeneratedSources)];
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(GeneratorDriverRunResult runResult)
    {
        return [.. runResult.Results.SelectMany(result => result.Diagnostics)];
    }

    private static Compilation CreateCompilation(string source)
    {
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.Serialization.DataContractAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location),
            MetadataReference.CreateFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"))
        ];

        return CSharpCompilation.Create(
            "compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
