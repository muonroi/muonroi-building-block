namespace Muonroi.CodeStandards.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.CodeStandards.Analyzers;
using Xunit;

public class Mstd0001_ForbiddenThrowAnalyzerTests
{
    [Fact]
    public void Mstd0001_ThrowRawArgumentException_InMuonroiNamespace_ShouldError()
    {
        string source = @"
namespace Muonroi.MyService
{
    public class MyService
    {
        public void DoWork(string input)
        {
            throw new System.ArgumentNullException(nameof(input));
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "MSTD0001");
    }

    [Fact]
    public void Mstd0001_ThrowExpressionRawException_InMuonroiNamespace_ShouldError()
    {
        string source = @"
namespace Muonroi.Core
{
    public class CoreService
    {
        public int GetValue() => throw new System.NotSupportedException();
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.Contains(diagnostics, d => d.Id == "MSTD0001");
    }

    [Fact]
    public void Mstd0001_ThrowMException_InMuonroiNamespace_ShouldNotError()
    {
        string source = @"
namespace Muonroi.Core.Abstractions.Exceptions
{
    public abstract class MException : System.Exception { }
    public class MNotFoundException : MException
    {
        public MNotFoundException(string message) : base(message) { }
    }
}

namespace Muonroi.MyService
{
    using Muonroi.Core.Abstractions.Exceptions;

    public class MyService
    {
        public void DoWork()
        {
            throw new MNotFoundException(""missing"");
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSTD0001");
    }

    [Fact]
    public void Mstd0001_ThrowRawException_InNonMuonroiNamespace_ShouldNotError()
    {
        string source = @"
namespace MyApp.Services
{
    public class AppService
    {
        public void Run() { throw new System.ArgumentException(""x""); }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSTD0001");
    }

    [Fact]
    public void Mstd0001_ThrowRawException_InTestAssembly_ShouldNotError()
    {
        string source = @"
namespace Muonroi.MyService
{
    public class MyServiceTest
    {
        public void TestMethod() { throw new System.ArgumentException(""x""); }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source, assemblyName: "MyProject.Tests");
        Assert.DoesNotContain(diagnostics, d => d.Id == "MSTD0001");
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string source, string assemblyName = "TestCompilation")
    {
        CSharpCompilation compilation = CreateCompilation(source, assemblyName);
        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mstd0001_ForbiddenThrowAnalyzer());
        return compilation.WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName)
    {
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll")),
        ];

        return CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
