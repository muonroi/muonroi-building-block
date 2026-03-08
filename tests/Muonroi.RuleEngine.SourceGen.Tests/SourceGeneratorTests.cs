namespace Muonroi.RuleEngine.SourceGen.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        var generator = new ExtractAsRuleGenerator();
        
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var runResult = driver.GetRunResult();
        Assert.NotEmpty(runResult.GeneratedTrees);
        Assert.Contains("RULE001", runResult.GeneratedTrees.First().ToString());
    }

    private static Compilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll")),
                MetadataReference.CreateFromFile(typeof(System.Runtime.Serialization.DataContractAttribute).Assembly.Location)
            },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
