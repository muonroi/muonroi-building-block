namespace Muonroi.RuleEngine.SourceGen.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Analyzers;
using Xunit;

/// <summary>
/// Unit tests for the MBB009 ForbiddenRawExceptionAnalyzer.
/// Verifies that raw throw new non-MException inside Muonroi.* namespaces produces an MBB009 warning,
/// and that MException subclasses, non-Muonroi namespaces, and test assemblies are exempt.
/// </summary>
public class MBB009_ForbiddenRawExceptionAnalyzerTests
{
    // ----------------------------------------------------------------
    // POSITIVE test 1: throw new ArgumentException in Muonroi.* namespace triggers MBB009
    // ----------------------------------------------------------------
    [Fact]
    public void MBB009_ThrowRawArgumentException_InMuonroiNamespace_ShouldWarn()
    {
        string source = @"
namespace Muonroi.MyService
{
    public class MyService
    {
        public void DoWork(string input)
        {
            throw new System.ArgumentException(""bad input"");
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB009");
    }

    // ----------------------------------------------------------------
    // POSITIVE test 2: throw new InvalidOperationException in Muonroi.Data namespace triggers MBB009
    // ----------------------------------------------------------------
    [Fact]
    public void MBB009_ThrowRawInvalidOperationException_InMuonroiNamespace_ShouldWarn()
    {
        string source = @"
namespace Muonroi.Data
{
    public class DataService
    {
        public void Load()
        {
            throw new System.InvalidOperationException(""oops"");
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB009");
    }

    // ----------------------------------------------------------------
    // POSITIVE test 3: expression-body throw new NotSupportedException in Muonroi.Core triggers MBB009
    // ----------------------------------------------------------------
    [Fact]
    public void MBB009_ThrowExpressionRawException_InMuonroiNamespace_ShouldWarn()
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

        Assert.Contains(diagnostics, d => d.Id == "MBB009");
    }

    // ----------------------------------------------------------------
    // NEGATIVE test 4: throw new MArgumentException (MException subclass) does NOT trigger MBB009
    // ----------------------------------------------------------------
    [Fact]
    public void MBB009_ThrowMArgumentException_InMuonroiNamespace_ShouldNotWarn()
    {
        // Provide MException and MArgumentException stubs in the test source
        string source = @"
namespace Muonroi.Core.Abstractions.Exceptions
{
    public abstract class MException : System.Exception { }
    public class MArgumentException : MException
    {
        public MArgumentException(string message) : base(message) { }
    }
}

namespace Muonroi.MyService
{
    using Muonroi.Core.Abstractions.Exceptions;

    public class MyService
    {
        public void DoWork(string input)
        {
            throw new MArgumentException(""bad input"");
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB009");
    }

    // ----------------------------------------------------------------
    // NEGATIVE test 5: throw new ArgumentException in non-Muonroi namespace does NOT trigger MBB009
    // ----------------------------------------------------------------
    [Fact]
    public void MBB009_ThrowRawException_InNonMuonroiNamespace_ShouldNotWarn()
    {
        string source = @"
namespace MyApp.Services
{
    public class AppService
    {
        public void Run()
        {
            throw new System.ArgumentException(""something wrong"");
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB009");
    }

    // ----------------------------------------------------------------
    // NEGATIVE test 6: throw new ArgumentException in assembly with ".Tests" in name does NOT trigger MBB009
    // ----------------------------------------------------------------
    [Fact]
    public void MBB009_ThrowRawException_InTestAssembly_ShouldNotWarn()
    {
        string source = @"
namespace Muonroi.MyService
{
    public class MyServiceTest
    {
        public void TestMethod()
        {
            throw new System.ArgumentException(""test exception"");
        }
    }
}
";
        // Use assembly name that contains ".Tests" — analyzer should skip this
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source, assemblyName: "MyProject.Tests");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB009");
    }

    // ----------------------------------------------------------------
    // Helper: run analyzer and collect diagnostics
    // ----------------------------------------------------------------
    private static ImmutableArray<Diagnostic> GetDiagnostics(string source, string assemblyName = "TestCompilation")
    {
        CSharpCompilation compilation = CreateCompilation(source, assemblyName);
        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mbb009_ForbiddenRawExceptionAnalyzer());

        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestCompilation")
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
