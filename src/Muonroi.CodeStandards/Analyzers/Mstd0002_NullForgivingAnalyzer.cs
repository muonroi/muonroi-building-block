using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.CodeStandards.Diagnostics;

namespace Muonroi.CodeStandards.Analyzers;

/// <summary>
/// MSTD0002: Forbids the null-forgiving operator '!' (SuppressNullableWarningExpression)
/// applied to a real expression (e.g. <c>product!.Id</c>, <c>result!</c>) inside Muonroi.*
/// non-test namespaces. Validate with MGuard.NotNull instead.
///
/// Does NOT fire on the placeholder forms <c>null!</c> / <c>default!</c> / <c>default(T)!</c>
/// (e.g. <c>object param = null!</c> declared to match an external override signature): those
/// declare a known-null/default value rather than suppressing a real nullable dereference.
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

        // Skip placeholder forms: 'null!', 'default!', and 'default(T)!' declare a known
        // null/default value (e.g. 'object param = null!' to match an external override
        // signature) and are NOT a dangerous null-forgiving dereference of a real value.
        if (context.Node is PostfixUnaryExpressionSyntax postfix)
        {
            ExpressionSyntax operand = postfix.Operand;
            if (operand.IsKind(SyntaxKind.NullLiteralExpression)
                || operand.IsKind(SyntaxKind.DefaultLiteralExpression)
                || operand.IsKind(SyntaxKind.DefaultExpression))
            {
                return;
            }
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
