using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators.Tests;

/// <summary>
/// Shared helper that wraps CSharpAnalyzerTest setup for all MSP0xx analyzer tests.
/// </summary>
/// <typeparam name="TAnalyzer">The Roslyn DiagnosticAnalyzer to test.</typeparam>
public static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// Creates a <see cref="DiagnosticResult"/> template for the given diagnostic ID.
    /// Callers chain .WithLocation() and .WithArguments() to complete the expectation.
    /// </summary>
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

    /// <summary>
    /// Runs the analyzer against <paramref name="source"/> and asserts that exactly
    /// the <paramref name="expected"/> diagnostics are produced.
    /// </summary>
    public static async Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            // Use net8.0 reference assemblies so the test harness compilation succeeds.
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync(CancellationToken.None);
    }
}
