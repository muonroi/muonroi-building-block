namespace Muonroi.RuleEngine.SourceGen.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Analyzers;
using Xunit;

/// <summary>
/// Unit tests for the MBB010 MissingGuardAnalyzer.
/// Verifies that public methods with unguarded non-nullable reference-type parameters
/// produce MBB010 warnings, and that guarded/private/nullable/value-type params are exempt.
/// </summary>
public class MBB010_MissingGuardAnalyzerTests
{
    // ----------------------------------------------------------------
    // POSITIVE Test 1: public method with unguarded string param triggers MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_StringParam_NoGuard_ShouldWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input) { var x = input.Length; }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // POSITIVE Test 2: multiple unguarded params each produce a diagnostic
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_MultipleUnguardedParams_ShouldWarnForEach()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string a, object b) { }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        // One diagnostic per unguarded parameter
        int mbb010Count = 0;
        foreach (Diagnostic d in diagnostics)
        {
            if (d.Id == "MBB010")
            {
                mbb010Count++;
            }
        }
        Assert.Equal(2, mbb010Count);
    }

    // ----------------------------------------------------------------
    // POSITIVE Test 3: public method with interface param (no guard) triggers MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_InterfaceParam_NoGuard_ShouldWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public interface IFoo { }
    public class MyService
    {
        public void Process(IFoo foo) { }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 4: private method does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PrivateMethod_NoGuard_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        private void Process(string input) { }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 5: nullable reference-type param (T?) does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_NullableParam_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string? input) { }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 6: value-type param (int) does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_ValueTypeParam_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(int count) { }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 7: MGuard.NotNull in body → does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_WithMGuardNotNull_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input) { MGuard.NotNull(input, nameof(input)); }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 8: if (param is null) guard → does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_WithIfNullCheck_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input) { if (input is null) throw new System.Exception(); }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 9: param with default null does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_ParamWithDefaultNull_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input = null) { }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 10: test assembly (contains ".Tests" in assembly name) does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_TestAssembly_ShouldNotWarn()
    {
        // Same source as positive test 1, but assembly name contains ".Tests"
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input) { var x = input.Length; }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source, assemblyName: "MyProject.Tests");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 11: ArgumentNullException.ThrowIfNull guard → does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_WithArgumentNullExceptionThrowIfNull_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input) { ArgumentNullException.ThrowIfNull(input, nameof(input)); }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // NEGATIVE Test 12: null-coalescing throw guard → does NOT trigger MBB010
    // ----------------------------------------------------------------
    [Fact]
    public void MBB010_PublicMethod_WithNullCoalescingThrow_ShouldNotWarn()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input) { var s = input ?? throw new System.ArgumentNullException(nameof(input)); }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB010");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private static ImmutableArray<Diagnostic> GetDiagnostics(string source, string assemblyName = "TestCompilation")
    {
        CSharpCompilation compilation = CreateCompilation(source, assemblyName);
        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mbb010_MissingGuardAnalyzer());

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

        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest);

        return CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }
}
