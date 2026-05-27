namespace Muonroi.RuleEngine.SourceGen.Tests;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Muonroi.RuleEngine.SourceGenerators.Analyzers;
using Xunit;

/// <summary>
/// Unit tests for the MBB005 AbstractionInfraDepAnalyzer.
/// Verifies an *.Abstractions assembly is flagged when it references an infrastructure
/// dependency — including infra packages that ship under the Microsoft.* namespace
/// (e.g. Microsoft.EntityFrameworkCore), which a naive "skip Microsoft.*" guard would miss.
/// </summary>
public class MBB005_AbstractionInfraDepAnalyzerTests
{
    // POSITIVE — regression guard for the Microsoft.* blind spot: EF Core ships as
    // Microsoft.EntityFrameworkCore and MUST still be flagged inside an .Abstractions assembly.
    [Fact]
    public void MBB005_AbstractionsAssembly_ReferencingEfCore_ShouldWarn()
    {
        ImmutableArray<Diagnostic> diagnostics =
            Analyze("Acme.Sample.Abstractions", "Microsoft.EntityFrameworkCore");

        Assert.Contains(diagnostics, d => d.Id == "MBB005");
    }

    // POSITIVE — a non-Microsoft infra token (MassTransit) is still flagged (original behavior).
    [Fact]
    public void MBB005_AbstractionsAssembly_ReferencingMassTransit_ShouldWarn()
    {
        ImmutableArray<Diagnostic> diagnostics =
            Analyze("Acme.Sample.Abstractions", "MassTransit");

        Assert.Contains(diagnostics, d => d.Id == "MBB005");
    }

    // NEGATIVE — benign Microsoft.* framework references must NOT be flagged, proving the
    // blind-spot fix does not over-trigger on the whole Microsoft namespace.
    [Fact]
    public void MBB005_AbstractionsAssembly_ReferencingMicrosoftExtensions_ShouldNotWarn()
    {
        ImmutableArray<Diagnostic> diagnostics = Analyze(
            "Acme.Sample.Abstractions",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB005");
    }

    // NEGATIVE — only *.Abstractions assemblies are gated; an implementation assembly may
    // reference EF Core freely.
    [Fact]
    public void MBB005_NonAbstractionsAssembly_ReferencingEfCore_ShouldNotWarn()
    {
        ImmutableArray<Diagnostic> diagnostics =
            Analyze("Acme.Sample.Services", "Microsoft.EntityFrameworkCore");

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB005");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private static ImmutableArray<Diagnostic> Analyze(string assemblyName, params string[] referencedAssemblyNames)
    {
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            .. referencedAssemblyNames.Select(CreateStubAssembly),
        ];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText("namespace T { public class C { } }")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mbb005_AbstractionInfraDepAnalyzer());

        return compilation
            .WithAnalyzers(analyzers)
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>Emits an empty in-memory assembly with the given identity so it appears in the
    /// compilation's referenced-assembly names without taking a real package dependency.</summary>
    private static MetadataReference CreateStubAssembly(string assemblyName)
    {
        CSharpCompilation stub = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText("namespace Stub { public class Marker { } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream ms = new();
        EmitResult result = stub.Emit(ms);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                "Stub assembly emit failed: " + string.Join("; ", result.Diagnostics.Select(d => d.ToString())));
        }

        return MetadataReference.CreateFromImage(ms.ToArray());
    }
}
