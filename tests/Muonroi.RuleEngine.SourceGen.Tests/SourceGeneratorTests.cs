namespace Muonroi.RuleEngine.SourceGen.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators;
using Xunit;

public class SourceGeneratorTests
{
    [Fact]
    public void ExtractAsRuleGenerator_ShouldGenerateRule()
    {
        // Arrange
        string source = @"
using System;

namespace TestNamespace;

public class MExtractAsRuleAttribute : Attribute 
{
    public MExtractAsRuleAttribute(string code) {}
    public int Order { get; set; }
}

public class TestRules
{
    [MExtractAsRule(""RULE001"")]
    public void MyRule(string context)
    {
        Console.WriteLine(context);
    }
}
";
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CreateDriver();

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var runResult = driver.GetRunResult();
        Assert.NotEmpty(runResult.GeneratedTrees);
        Assert.Contains("RULE001", runResult.GeneratedTrees.First().ToString());
    }

    [Fact]
    public void ExtractAsRuleGenerator_ShouldGenerateAuthoringManifestProvider()
    {
        string source = @"
using System;

namespace TestNamespace;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
    public int Order { get; set; }
}

public sealed class FactBag
{
    public T? Get<T>(string key) => default;
    public void Set<T>(string key, T value) {}
}

public sealed class OrderRequest
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class OrderContext
{
    public OrderRequest Request { get; set; } = new();
}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_ORDER_VALIDATE"", Order = 1)]
    public void ValidateOrder(OrderContext context, FactBag facts)
    {
        var existing = facts.Get<string>(""existingStatus"");
        facts.Set(""validatedOrderId"", context.Request.OrderId);
    }
}
";
        Compilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        string generated = string.Join("\n\n", GetGeneratedSources(driver.GetRunResult()).Select(item => item.SourceText.ToString()));

        Assert.Contains("GeneratedManifestProvider", generated);
        Assert.Contains("RULE_ORDER_VALIDATE", generated);
        Assert.Contains("validatedOrderId", generated);
        Assert.Contains("existingStatus", generated);
    }

    [Fact]
    public void ExtractAsRuleGenerator_ShouldGenerateNoOpExecuteAsyncForExtractedRules()
    {
        string source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestNamespace;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
    public int Order { get; set; }
}

public sealed class FactBag
{
    public void Set<T>(string key, T value) {}
}

public sealed class OrderContext
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_ORDER_CAPTURE"", Order = 1)]
    public void Capture(OrderContext context, FactBag facts)
    {
        facts.Set(""capturedOrderId"", context.OrderId);
    }
}
";

        Compilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        string generated = string.Join("\n\n", GetGeneratedSources(driver.GetRunResult()).Select(item => item.SourceText.ToString()));

        Assert.Contains("public Task ExecuteAsync(OrderContext context, CancellationToken cancellationToken = default)", generated);
        Assert.DoesNotContain("_ = await EvaluateAsync(context, new FactBag(), cancellationToken);", generated);
        Assert.DoesNotContain("public async Task ExecuteAsync(OrderContext context, CancellationToken cancellationToken = default)", generated);
    }

    [Fact]
    public void ExtractAsRuleGenerator_ShouldUseSystemTypeAndSkipAuthoringAttributesOnGeneratedRules()
    {
        string source = @"
using System;
using Muonroi.RuleEngine.Abstractions.Authoring;

namespace Muonroi.RuleEngine.Abstractions.Authoring
{
    public sealed class MRuleContextDescriptionAttribute : Attribute
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
    }

    public sealed class MRuleFactDescriptionAttribute : Attribute
    {
        public MRuleFactDescriptionAttribute(string factKey) {}
        public string? Label { get; set; }
    }
}

namespace TestNamespace
{
    public class MExtractAsRuleAttribute : Attribute
    {
        public MExtractAsRuleAttribute(string code) {}
        public int Order { get; set; }
    }

    public sealed class FactBag
    {
        public void Set<T>(string key, T value) {}
    }

    public sealed class OrderContext
    {
        public string OrderId { get; set; } = string.Empty;
    }

    public sealed class TestRules
    {
        [MExtractAsRule(""RULE_ORDER_AUTHORING"", Order = 1)]
        [MRuleContextDescription(Title = ""Order create"")]
        [MRuleFactDescription(""facts.orderId"", Label = ""Order Id"")]
        public void Capture(OrderContext context, FactBag facts)
        {
            facts.Set(""facts.orderId"", context.OrderId);
        }
    }
}
";

        Compilation compilation = CreateCompilation(source);
        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        string generated = string.Join("\n\n", GetGeneratedSources(driver.GetRunResult()).Select(item => item.SourceText.ToString()));

        Assert.Contains("IEnumerable<System.Type>", generated);
        Assert.DoesNotContain("MRuleContextDescriptionAttribute", generated);
        Assert.DoesNotContain("MRuleFactDescriptionAttribute", generated);
        Assert.Contains("GeneratedManifestProvider", generated);
    }

    [Fact]
    public void ExtractAsRuleGenerator_DiagnosticsOnly_ShouldStillEmitManifestButSkipRuntimeRules()
    {
        string source = @"
using System;

namespace TestNamespace;

public class MExtractAsRuleAttribute : Attribute
{
    public MExtractAsRuleAttribute(string code) {}
}

public sealed class FactBag
{
    public void Set<T>(string key, T value) {}
}

public sealed class OrderContext
{
    public string OrderId { get; set; } = string.Empty;
}

public sealed class TestRules
{
    [MExtractAsRule(""RULE_ORDER_MANIFEST_ONLY"")]
    public void Capture(OrderContext context, FactBag facts)
    {
        facts.Set(""facts.orderId"", context.OrderId);
    }
}
";

        Compilation compilation = CreateCompilation(source);
        AnalyzerConfigOptionsProvider optionsProvider = new TestAnalyzerConfigOptionsProvider(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.MuonroiRuleGenDiagnosticsOnly"] = "true"
            });

        GeneratorDriver driver = CreateDriver((CSharpParseOptions?)compilation.SyntaxTrees.First().Options, optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        string generated = string.Join("\n\n", GetGeneratedSources(driver.GetRunResult()).Select(item => item.SourceText.ToString()));

        Assert.DoesNotContain("RULE_ORDER_MANIFEST_ONLYRule", generated);
        Assert.Contains("GeneratedManifestProvider", generated);
        Assert.Contains("RULE_ORDER_MANIFEST_ONLY", generated);
    }

    private static GeneratorDriver CreateDriver(CSharpParseOptions? parseOptions = null, AnalyzerConfigOptionsProvider? optionsProvider = null)
    {
        IIncrementalGenerator[] generators =
        [
            new ExtractAsRuleGenerator(),
            new RuleCatalogRegistrationGenerator()
        ];

        return CSharpGeneratorDriver.Create(
            generators: generators.Select(generator => generator.AsSourceGenerator()),
            additionalTexts: ImmutableArray<AdditionalText>.Empty,
            parseOptions: parseOptions,
            optionsProvider: optionsProvider);
    }

    private static ImmutableArray<GeneratedSourceResult> GetGeneratedSources(GeneratorDriverRunResult runResult)
    {
        return [.. runResult.Results.SelectMany(result => result.GeneratedSources)];
    }

    private static Compilation CreateCompilation(string source)
    {
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(System.Runtime.Serialization.DataContractAttribute).Assembly.Location)
        ];

        return CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed class TestAnalyzerConfigOptionsProvider(IDictionary<string, string> globalOptions) : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _globalOptions = new DictionaryAnalyzerConfigOptions(globalOptions);
        private static readonly AnalyzerConfigOptions EmptyOptions = new DictionaryAnalyzerConfigOptions(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => EmptyOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => EmptyOptions;
    }

    private sealed class DictionaryAnalyzerConfigOptions(IDictionary<string, string> values) : AnalyzerConfigOptions
    {
        private readonly IDictionary<string, string> _values = values;

        public override bool TryGetValue(string key, out string value)
        {
            if (_values.TryGetValue(key, out string? existing))
            {
                value = existing;
                return true;
            }

            value = string.Empty;
            return false;
        }
    }
}
