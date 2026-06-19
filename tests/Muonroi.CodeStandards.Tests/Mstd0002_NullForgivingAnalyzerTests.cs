namespace Muonroi.CodeStandards.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.CodeStandards.Analyzers;
using Xunit;

public class Mstd0002_NullForgivingAnalyzerTests
{
    [Fact]
    public void Mstd0002_NullForgivingMemberAccess_InMuonroiNamespace_ShouldError()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class Product { public int Id; }
    public class MyService
    {
        public int Read(Product? product) { return product!.Id; }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "MSTD0002");
    }

    [Fact]
    public void Mstd0002_BareNullForgiving_InMuonroiNamespace_ShouldError()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public string Read(string? s) { return s!; }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "MSTD0002");
    }

    [Fact]
    public void Mstd0002_NoNullForgiving_ShouldNotError()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public string Read(string s) { return s; }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSTD0002");
    }

    [Fact]
    public void Mstd0002_NullForgiving_InNonMuonroiNamespace_ShouldNotError()
    {
        string source = @"
#nullable enable
namespace MyApp.Services
{
    public class AppService
    {
        public string Read(string? s) { return s!; }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSTD0002");
    }

    [Fact]
    public void Mstd0002_NullForgiving_InTestAssembly_ShouldNotError()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyServiceTest
    {
        public string Read(string? s) { return s!; }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source, assemblyName: "MyProject.Tests");
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSTD0002");
    }

    [Fact]
    public void Mstd0002_NullForgiving_SuppressedByPragma_ShouldNotError()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
#pragma warning disable MSTD0002
        public string Read(string? s) { return s!; }
#pragma warning restore MSTD0002
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.DoesNotContain(diagnostics.Where(d => !d.IsSuppressed), d => d.Id == "MSTD0002");
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string source, string assemblyName = "TestCompilation")
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest);

        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll")),
        ];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mstd0002_NullForgivingAnalyzer());
        return compilation.WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }
}
