using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.CodeStandards.Diagnostics;

namespace Muonroi.CodeStandards.Analyzers;

/// <summary>
/// MSTD0002: Forbids the null-forgiving operator '!' (SuppressNullableWarningExpression)
/// inside Muonroi.* non-test namespaces. Validate with MGuard.NotNull instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mstd0002_NullForgivingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MstdDiagnosticDescriptors.Mstd0002NullForgiving);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (MstdAnalyzerHelpers.IsTestAssembly(context.Compilation))
        {
            return;
        }

        string ns = MstdAnalyzerHelpers.GetNamespace(context.Node);
        if (!MstdAnalyzerHelpers.IsMuonroiNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MstdDiagnosticDescriptors.Mstd0002NullForgiving,
            context.Node.GetLocation(),
            ns));
    }
}
